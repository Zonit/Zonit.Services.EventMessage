using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Zonit.Services.EventMessage;

/// <summary>
/// Usługa hostowana, która inicjalizuje wszystkie handlery zadań przy starcie aplikacji
/// </summary>
public class TaskHandlerRegistrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskHandlerRegistrationHostedService> _logger;

    public TaskHandlerRegistrationHostedService(
        IServiceProvider serviceProvider,
        ILogger<TaskHandlerRegistrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing task handlers");

        using (var scope = _serviceProvider.CreateScope())
        {
            var initializers = scope.ServiceProvider.GetServices<ITaskHandlerInitializer>().ToList();

            _logger.LogInformation("Found {Count} task handlers to initialize", initializers.Count);

            foreach (var initializer in initializers)
            {
                try
                {
                    initializer.Initialize();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing task handler");
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}