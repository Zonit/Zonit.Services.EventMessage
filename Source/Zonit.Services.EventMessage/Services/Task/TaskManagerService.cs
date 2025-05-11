using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Zonit.Services.EventMessage.Services;

internal class TaskManagerService : ITaskManager, IDisposable
{
    public static int DefaultMaxConcurrentTasks = 1000;

    /// <summary>
    /// Determines whether completed tasks should be automatically removed from the collection
    /// </summary>
    public static bool AutoRemoveCompletedTasks = true;

    public static int DefaultWorkes = 10;

    public static TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, TaskEventModel> _tasks = new();
    private readonly ConcurrentBag<Action<TaskEventModel>> _changeSubscribers = new();
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();
    private readonly ConcurrentDictionary<Type, int> _workers = new();
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _semaphores = new();
    private readonly ConcurrentDictionary<Type, TimeSpan> _timeouts = new();
    private readonly object _subscriberLock = new();
    private bool _disposed;

    readonly ILogger<TaskManagerService> _logger;


    public TaskManagerService(ILogger<TaskManagerService> logger,
        int defaultMaxConcurrentTasks = 1,
        bool autoRemoveCompletedTasks = true,
        TimeSpan? defaultTimeout = null)
    {
        _logger = logger;
        DefaultMaxConcurrentTasks = defaultMaxConcurrentTasks;
        AutoRemoveCompletedTasks = autoRemoveCompletedTasks;
        DefaultTimeout = defaultTimeout ?? TimeSpan.FromMinutes(5);

        if (AutoRemoveCompletedTasks)
        {
            _ = StartPeriodicCleanupAsync(DefaultTimeout);
        }
    }


    public void Subscribe<TModel>(Func<PayloadModel<TModel>, Task> handler, int? workers = null, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        workers ??= DefaultWorkes;
        timeout ??= DefaultTimeout;

        if (workers <= 0)
            throw new ArgumentOutOfRangeException(nameof(workers), "Number of workers must be greater than zero.");

        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");

        Type modelType = typeof(TModel);

        // Check if a handler for this model type already exists
        if (_handlers.ContainsKey(modelType))
        {
            throw new InvalidOperationException(
                $"Handler for type {modelType.Name} already exists. Only one handler can be registered per model type.");
        }

        _logger?.LogInformation("Registered Task handler for type: {TypeName} with workers: {Workers} and timeout: {Timeout}", modelType.Name, workers, timeout);

        // Register new handler for the specific type
        var handlersList = new List<object> { handler };
        if (!_handlers.TryAdd(modelType, handlersList))
        {
            throw new InvalidOperationException(
                $"Cannot add handler for type {modelType.Name}. Possible concurrency conflict.");
        }

        // Configure semaphore for this task type
        var semaphore = new SemaphoreSlim(workers ?? DefaultWorkes, workers ?? DefaultWorkes);
        if (!_semaphores.TryAdd(modelType, semaphore))
        {
            // If failed to add semaphore, remove the added handler
            _handlers.TryRemove(modelType, out _);
            semaphore.Dispose();
            throw new InvalidOperationException(
                $"Cannot configure semaphore for type {modelType.Name}. Possible concurrency conflict.");
        }

        // Save the number of workers for this task type
        _workers[modelType] = workers ?? DefaultWorkes;

        // Save the timeout for this task type
        _timeouts[modelType] = timeout ?? DefaultTimeout;
    }

    public void EventOnChange(Action<TaskEventModel> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        _changeSubscribers.Add(change);
    }

    public IReadOnlyCollection<TaskEventModel> GetActiveTasks(Guid? extensionId = null)
    {
        return _tasks.Values
            .Where(t => t.Status is not (TaskEventStatus.Completed or TaskEventStatus.Failed or TaskEventStatus.Cancelled) &&
                        (extensionId == null || t.ExtensionId == extensionId))
            .OrderBy(t => t.CreatedAt)
            .ToList()
            .AsReadOnly();
    }

    internal void NotifyChange(TaskEventModel task)
    {
        ArgumentNullException.ThrowIfNull(task);

        // Create a local copy of subscribers to avoid issues with collection modification during iteration
        var subscribers = _changeSubscribers.ToArray();

        foreach (var subscriber in subscribers)
        {
            try
            {
                subscriber(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while notifying subscriber about task change {TaskId}", task.Id);
            }
        }
    }

    public void Publish(TaskEventModel task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (_tasks.TryAdd(task.Id, task))
        {
            NotifyChange(task);
            _ = ProcessTaskAsync(task);
        }
    }

    private async Task ProcessTaskAsync(TaskEventModel task)
    {
        Type dataType = task.Payload.Data.GetType();
        SemaphoreSlim? semaphore = null;

        try
        {
            _logger.LogInformation("Processing task: {TaskId}, data type: {DataType}", task.Id, dataType.Name);

            // Get the appropriate semaphore for the data type or use default
            if (!_semaphores.TryGetValue(dataType, out semaphore))
            {
                _logger.LogWarning("No defined semaphore for type {DataType}, using default", dataType.Name);
                semaphore = new SemaphoreSlim(DefaultMaxConcurrentTasks, DefaultMaxConcurrentTasks);
                _semaphores[dataType] = semaphore;
            }

            // Get timeout for this task type or use default
            TimeSpan timeout = _timeouts.TryGetValue(dataType, out var configuredTimeout)
                ? configuredTimeout
                : DefaultTimeout; // Default timeout of 5 minutes

            // Wait for available space in the pool
            bool acquired = await semaphore.WaitAsync(timeout);
            if (!acquired)
            {
                _logger.LogWarning("Task {TaskId} timed out while waiting for execution slot", task.Id);
                task.Status = TaskEventStatus.Failed;
                NotifyChange(task);
                return;
            }

            try
            {
                // Update status
                task.Status = TaskEventStatus.Processing;
                task.ProcessedAt = DateTime.UtcNow;
                NotifyChange(task);

                // Check if we have a handler for this type
                if (!_handlers.TryGetValue(dataType, out var handlers) || handlers.Count == 0)
                {
                    _logger.LogWarning("No handler for type {DataType} in task {TaskId}", dataType.Name, task.Id);
                    task.Status = TaskEventStatus.Failed;
                    return;
                }

                // Since we now have only one handler per type, take the first from the list
                var handler = handlers[0];
                _logger.LogDebug("Executing handler for type: {DataType}", dataType.Name);

                // Create PayloadModel<TModel> object
                var genericPayloadType = typeof(PayloadModel<>).MakeGenericType(dataType);
                object genericPayload;

                try
                {
                    // Try to use constructor with parameters
                    genericPayload = Activator.CreateInstance(
                        genericPayloadType,
                        task.Payload.Data,
                        task.Payload.CancellationToken)
                        ?? throw new InvalidOperationException($"Failed to create instance of {genericPayloadType.Name}");
                }
                catch (MissingMethodException)
                {
                    // If constructor doesn't exist, use properties
                    _logger.LogWarning("No appropriate constructor for {PayloadType}, using properties", genericPayloadType.Name);
                    genericPayload = Activator.CreateInstance(genericPayloadType)!;

                    // Set properties
                    PropertyInfo? dataProperty = genericPayloadType.GetProperty("Data")
                        ?? throw new InvalidOperationException($"Property Data not found in {genericPayloadType.Name}");

                    PropertyInfo? tokenProperty2 = genericPayloadType.GetProperty("CancellationToken")
                        ?? throw new InvalidOperationException($"Property CancellationToken not found in {genericPayloadType.Name}");

                    dataProperty.SetValue(genericPayload, task.Payload.Data);
                    tokenProperty2.SetValue(genericPayload, task.Payload.CancellationToken);
                }

                // Find Invoke method for handler
                MethodInfo? invokeMethod = handler.GetType().GetMethod("Invoke")
                    ?? throw new InvalidOperationException($"Method Invoke not found for handler {handler.GetType().Name}");

                // Set up timeout token source
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCts.Token,
                    task.Payload.CancellationToken);

                // Override cancellation token in payload with linked token
                PropertyInfo? tokenProperty = genericPayloadType.GetProperty("CancellationToken");
                tokenProperty?.SetValue(genericPayload, linkedCts.Token);

                // Call handler
                var result = invokeMethod.Invoke(handler, new[] { genericPayload });
                if (result is Task taskResult)
                {
                    try
                    {
                        await taskResult;
                        _logger.LogDebug("Handler executed successfully");
                        task.Status = TaskEventStatus.Completed;
                    }
                    catch (OperationCanceledException)
                    {
                        if (timeoutCts.IsCancellationRequested)
                        {
                            _logger.LogWarning("Task {TaskId} execution timed out after {Timeout}", task.Id, timeout);
                            task.Status = TaskEventStatus.Failed;
                        }
                        else
                        {
                            _logger.LogInformation("Task {TaskId} was cancelled", task.Id);
                            task.Status = TaskEventStatus.Cancelled;
                        }
                    }
                }
                else
                {
                    _logger.LogError("Invoke method did not return a Task object");
                    task.Status = TaskEventStatus.Failed;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Task {TaskId} was cancelled", task.Id);
                task.Status = TaskEventStatus.Cancelled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing task {TaskId}", task.Id);
                task.Status = TaskEventStatus.Failed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing task {TaskId}", task.Id);
            task.Status = TaskEventStatus.Failed;
        }
        finally
        {
            // Complete task
            task.CompletedAt = DateTime.UtcNow;
            NotifyChange(task);

            // Release space in the pool
            semaphore?.Release();

            // Remove task from collection if completed and AutoRemoveCompletedTasks is true
            if (AutoRemoveCompletedTasks && (
                task.Status == TaskEventStatus.Completed ||
                task.Status == TaskEventStatus.Failed ||
                task.Status == TaskEventStatus.Cancelled))
            {
                _logger.LogDebug("Removing completed task: {TaskId} with status {Status}", task.Id, task.Status);
                _tasks.TryRemove(task.Id, out _);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void UnsubscribeAll()
    {
        _logger.LogInformation("Removing all handlers");
        _handlers.Clear();

        // Release all semaphores
        foreach (var semaphore in _semaphores.Values)
        {
            try
            {
                semaphore.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while releasing semaphore");
            }
        }
        _semaphores.Clear();
        _workers.Clear();
        _timeouts.Clear();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            UnsubscribeAll();
            _changeSubscribers.Clear();
            _tasks.Clear();
            _disposed = true;
        }
    }

    public void Unsubscribe<TModel>()
    {
        Type modelType = typeof(TModel);

        _logger.LogInformation("Removing handler for type: {TypeName}", modelType.Name);

        if (_handlers.TryRemove(modelType, out _))
        {
            // Handler removed, now remove semaphore
            if (_semaphores.TryRemove(modelType, out var semaphore))
            {
                semaphore.Dispose();
            }

            _workers.TryRemove(modelType, out _);
            _timeouts.TryRemove(modelType, out _);
        }
        else
        {
            _logger.LogWarning("No handler found for type: {TypeName}", modelType.Name);
        }
    }

    /// <summary>
    /// Cleans up completed tasks from the collection
    /// </summary>
    /// <returns>Number of removed tasks</returns>
    public int CleanupCompletedTasks()
    {
        int removedCount = 0;

        var completedTaskIds = _tasks.Values
            .Where(t => t.Status == TaskEventStatus.Completed ||
                        t.Status == TaskEventStatus.Failed ||
                        t.Status == TaskEventStatus.Cancelled)
            .Select(t => t.Id)
            .ToList();

        foreach (var taskId in completedTaskIds)
        {
            if (_tasks.TryRemove(taskId, out _))
            {
                removedCount++;
            }
        }

        _logger.LogInformation("Removed {RemovedCount} completed tasks", removedCount);
        return removedCount;
    }

    private async Task StartPeriodicCleanupAsync(TimeSpan interval)
    {
        while (!_disposed)
        {
            await Task.Delay(interval);

            if (_disposed)
                break;

            try
            {
                CleanupCompletedTasks();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic task cleanup");
            }
        }
    }

    /// <summary>
    /// Process tasks in chronological order (oldest first)
    /// </summary>
    public void ProcessTasksChronologically()
    {
        // Get all pending tasks sorted by creation date
        var pendingTasks = _tasks.Values
            .Where(t => t.Status == TaskEventStatus.Pending)
            .OrderBy(t => t.CreatedAt)
            .ToList();

        foreach (var task in pendingTasks)
        {
            // Only start processing if still in pending state
            // This check is needed since the task might have been processed by another thread
            if (task.Status == TaskEventStatus.Pending)
            {
                _ = ProcessTaskAsync(task);
            }
        }
    }
}