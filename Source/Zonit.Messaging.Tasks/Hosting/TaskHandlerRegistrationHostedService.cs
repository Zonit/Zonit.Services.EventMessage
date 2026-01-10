using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Zonit.Messaging.Tasks.Hosting;

/// <summary>
/// Hosted service do automatycznej rejestracji handlerów zadañ przy starcie aplikacji.
/// Skanuje DI po ITaskHandler&lt;T&gt; i automatycznie subskrybuje do odpowiednich zadañ.
/// </summary>
public sealed class TaskHandlerRegistrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITaskManager _taskManager;
    private readonly ILogger<TaskHandlerRegistrationHostedService> _logger;

    public TaskHandlerRegistrationHostedService(
        IServiceProvider serviceProvider,
        ITaskManager taskManager,
        ILogger<TaskHandlerRegistrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _taskManager = taskManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting task handler registration...");

        using var scope = _serviceProvider.CreateScope();
        var registrations = scope.ServiceProvider.GetServices<TaskHandlerRegistration>();

        foreach (var registration in registrations)
        {
            registration.Subscribe(_taskManager, _serviceProvider);
            _logger.LogDebug("Subscribed handler for task type '{TaskType}'", registration.TaskType.Name);
        }

        _logger.LogInformation("Task handler registration completed");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Abstrakcyjna rejestracja handlera zadañ.
/// </summary>
public abstract class TaskHandlerRegistration
{
    public abstract Type TaskType { get; }
    public abstract void Subscribe(ITaskManager taskManager, IServiceProvider serviceProvider);
}

/// <summary>
/// Typowana rejestracja handlera zadañ.
/// </summary>
public sealed class TaskHandlerRegistration<TTask> : TaskHandlerRegistration where TTask : notnull
{
    private readonly TaskSubscriptionOptions? _options;

    public TaskHandlerRegistration(TaskSubscriptionOptions? options = null)
    {
        _options = options;
    }

    public override Type TaskType => typeof(TTask);

    public override void Subscribe(ITaskManager taskManager, IServiceProvider serviceProvider)
    {
        taskManager.Subscribe<TTask>(async payload =>
        {
            using var scope = serviceProvider.CreateScope();
            var handlers = scope.ServiceProvider.GetServices<ITaskHandler<TTask>>();

            foreach (var handler in handlers)
            {
                await handler.HandleAsync(payload);
            }
        }, GetOptionsFromHandler(serviceProvider));
    }

    private TaskSubscriptionOptions GetOptionsFromHandler(IServiceProvider serviceProvider)
    {
        if (_options is not null)
            return _options;

        // Try to get options from handler
        using var scope = serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetService<ITaskHandler<TTask>>();
        
        if (handler is TaskHandler<TTask> typedHandler)
        {
            return new TaskSubscriptionOptions
            {
                WorkerCount = typedHandler.WorkerCount,
                Timeout = typedHandler.Timeout,
                ProgressSteps = typedHandler.ProgressSteps,
                Title = typedHandler.Title,
                Description = typedHandler.Description
            };
        }

        return new TaskSubscriptionOptions();
    }
}
