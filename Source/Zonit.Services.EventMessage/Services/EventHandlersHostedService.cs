using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Zonit.Services.EventMessage.Services;

internal class EventHandlersHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventHandlersHostedService> _logger;

    public EventHandlersHostedService(
        IServiceProvider serviceProvider,
        ILogger<EventHandlersHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting event handlers registration");

        try
        {
            // Używamy osobnego scope tylko do pobrania handlerów
            using var scope = _serviceProvider.CreateScope();
            var handlers = scope.ServiceProvider.GetServices<IEventHandler>().ToList();

            _logger.LogInformation("Found {Count} event handlers to register", handlers.Count);

            // Rejestrujemy każdy handler, używając root service provider
            // aby uniknąć problemów z disposowaniem scope
            foreach (var handler in handlers)
            {
                if (handler is EventBase eventBaseHandler)
                {
                    try
                    {
                        eventBaseHandler.Subscribe(_serviceProvider);
                        _logger.LogDebug("Registered event handler: {HandlerType}", handler.GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to register event handler: {HandlerType}", handler.GetType().Name);
                    }
                }
                else
                {
                    _logger.LogWarning("Handler {HandlerType} does not inherit from EventBase and will be skipped",
                        handler.GetType().Name);
                }
            }

            _logger.LogInformation("Event handlers registration completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register event handlers");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping event handlers service");
        return Task.CompletedTask;
    }
}
