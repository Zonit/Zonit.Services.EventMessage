using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Zonit.Messaging.Tasks;

/// <summary>
/// Domyœlna implementacja ITaskManager.
/// U¿ywa Channel do asynchronicznego przetwarzania zadañ w tle.
/// </summary>
public sealed class TaskManager : ITaskManager, IDisposable
{
    private readonly ConcurrentDictionary<string, List<TaskSubscription>> _subscriptions = new();
    private readonly TaskStateStore _stateStore = new();
    private readonly ILogger<TaskManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public TaskManager(ILogger<TaskManager> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        // Cleanup starych tasków co 5 minut (usuwa zakoñczone starsze ni¿ 30 min)
        _cleanupTimer = new Timer(
            _ => _stateStore.CleanupOldTasks(TimeSpan.FromMinutes(30)),
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    public void Publish<TTask>(TTask payload, Guid? extensionId = null) where TTask : notnull
    {
        var taskName = GetTaskName<TTask>();
        Publish(taskName, payload, extensionId);
    }

    public void Publish(string taskName, object payload, Guid? extensionId = null)
    {
        ArgumentNullException.ThrowIfNull(taskName);
        ArgumentNullException.ThrowIfNull(payload);

        if (!_subscriptions.TryGetValue(taskName, out var subscriptions))
        {
            _logger.LogDebug("No subscribers for task '{TaskName}'", taskName);
            return;
        }

        foreach (var subscription in subscriptions)
        {
            try
            {
                subscription.Enqueue(payload, extensionId, _stateStore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueueing task '{TaskName}'", taskName);
            }
        }
    }

    public void Subscribe<TTask>(Func<TaskPayload<TTask>, Task> handler, TaskSubscriptionOptions? options = null) 
        where TTask : notnull
    {
        var taskName = GetTaskName<TTask>();
        var opts = options ?? new TaskSubscriptionOptions();

        var subscription = new TaskSubscription<TTask>(handler, opts, _logger, opts.ProgressSteps);
        
        _subscriptions.AddOrUpdate(
            taskName,
            _ => [subscription],
            (_, list) => { list.Add(subscription); return list; });

        _logger.LogInformation(
            "Subscribed to task '{TaskName}' with {WorkerCount} workers", 
            taskName, 
            opts.WorkerCount);
    }


    public void Subscribe(string taskName, Func<TaskPayload<object>, Task> handler, TaskSubscriptionOptions? options = null)
    {
        var opts = options ?? new TaskSubscriptionOptions();
        var subscription = new TaskSubscription<object>(handler, opts, _logger, opts.ProgressSteps);

        _subscriptions.AddOrUpdate(
            taskName,
            _ => [subscription],
            (_, list) => { list.Add(subscription); return list; });

        _logger.LogInformation(
            "Subscribed to task '{TaskName}' with {WorkerCount} workers", 
            taskName, 
            opts.WorkerCount);
    }

    public IDisposable OnChange(Action<TaskState> handler)
    {
        return _stateStore.Subscribe(handler);
    }

    public IDisposable OnChange(Guid extensionId, Action<TaskState> handler)
    {
        return _stateStore.Subscribe(extensionId, handler);
    }

    public IDisposable OnChange<TTask>(Action<TaskState<TTask>> handler) where TTask : notnull
    {
        return _stateStore.Subscribe(handler);
    }

    public IDisposable OnChange<TTask>(Guid extensionId, Action<TaskState<TTask>> handler) where TTask : notnull
    {
        return _stateStore.Subscribe(extensionId, handler);
    }

    public IReadOnlyCollection<TaskState> GetActiveTasks(Guid? extensionId = null)
    {
        return _stateStore.GetActiveTasks(extensionId);
    }

    public TaskState? GetTaskState(Guid taskId)
    {
        return _stateStore.GetTaskState(taskId);
    }

    private static string GetTaskName<TTask>() => typeof(TTask).FullName ?? typeof(TTask).Name;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cleanupTimer.Dispose();
        
        foreach (var subscriptions in _subscriptions.Values)
        {
            foreach (var subscription in subscriptions)
            {
                subscription.Dispose();
            }
        }
        _subscriptions.Clear();
    }
}

/// <summary>
/// Wewnêtrzna klasa reprezentuj¹ca subskrypcjê zadania.
/// </summary>
internal abstract class TaskSubscription : IDisposable
{
    public abstract void Enqueue(object payload, Guid? extensionId, TaskStateStore stateStore);
    public abstract void Dispose();
}

/// <summary>
/// Typowana subskrypcja zadania.
/// </summary>
internal sealed class TaskSubscription<TTask> : TaskSubscription where TTask : notnull
{
    private readonly Channel<(TTask Data, Guid? ExtensionId, Guid TaskId)> _channel;
    private readonly Func<TaskPayload<TTask>, Task> _handler;
    private readonly TaskSubscriptionOptions _options;
    private readonly ILogger _logger;
    private readonly TaskProgressStep[]? _progressSteps;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task[] _workers;
    private TaskStateStore? _stateStore;

    public TaskSubscription(
        Func<TaskPayload<TTask>, Task> handler, 
        TaskSubscriptionOptions options, 
        ILogger logger,
        TaskProgressStep[]? progressSteps)
    {
        _handler = handler;
        _options = options;
        _logger = logger;
        _progressSteps = progressSteps;

        _channel = Channel.CreateUnbounded<(TTask, Guid?, Guid)>(new UnboundedChannelOptions
        {
            SingleReader = options.WorkerCount == 1,
            SingleWriter = false
        });

        _workers = new Task[options.WorkerCount];
        for (int i = 0; i < options.WorkerCount; i++)
        {
            _workers[i] = ProcessTasksAsync(_cts.Token);
        }
    }

    public override void Enqueue(object payload, Guid? extensionId, TaskStateStore stateStore)
    {
        _stateStore = stateStore;
        
        if (payload is TTask typedPayload)
        {
            var taskId = Guid.NewGuid();
            var totalSteps = _progressSteps?.Length;
            var taskType = typeof(TTask).FullName ?? typeof(TTask).Name;
            
            stateStore.CreateTask(taskId, taskType, extensionId, totalSteps, typedPayload);
            _channel.Writer.TryWrite((typedPayload, extensionId, taskId));
        }
        else
        {
            _logger.LogWarning(
                "Expected task type '{ExpectedType}', got '{ActualType}'",
                typeof(TTask).Name,
                payload.GetType().Name);
        }
    }

    private async Task ProcessTasksAsync(CancellationToken cancellationToken)
    {
        await foreach (var (data, extensionId, taskId) in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            var retryCount = 0;
            var success = false;

            _stateStore?.StartTask(taskId);

            while (!success && retryCount <= _options.MaxRetries)
            {
                TaskProgressContext? progressContext = null;
                try
                {
                    using var timeoutCts = new CancellationTokenSource(_options.Timeout);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        timeoutCts.Token, 
                        cancellationToken);

                    progressContext = new TaskProgressContext(
                        _progressSteps,
                        (progress, step, message) => _stateStore?.UpdateProgress(taskId, progress, step, message));

                    var payload = new TaskPayload<TTask>
                    {
                        Data = data,
                        TaskId = taskId,
                        CancellationToken = linkedCts.Token,
                        ExtensionId = extensionId,
                        Progress = progressContext
                    };

                    await _handler(payload);
                    success = true;
                    
                    progressContext.Dispose();
                    _stateStore?.CompleteTask(taskId);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    progressContext?.Dispose();
                    _stateStore?.CancelTask(taskId);
                    return;
                }
                catch (Exception ex)
                {
                    progressContext?.Dispose();
                    retryCount++;
                    
                    if (retryCount <= _options.MaxRetries)
                    {
                        _logger.LogWarning(ex, 
                            "Error processing task of type '{TaskType}', retry {RetryCount}/{MaxRetries}", 
                            typeof(TTask).Name, retryCount, _options.MaxRetries);
                        await Task.Delay(_options.RetryDelay, cancellationToken);
                    }
                    else
                    {
                        _logger.LogError(ex, "Error processing task of type '{TaskType}' after {RetryCount} retries", 
                            typeof(TTask).Name, retryCount);
                        
                        _stateStore?.FailTask(taskId);
                        
                        if (!_options.ContinueOnError)
                            throw;
                    }
                }
            }
        }
    }

    public override void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.Complete();
        
        try
        {
            Task.WhenAll(_workers).Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore cancellation exceptions during shutdown
        }

        _cts.Dispose();
    }
}
