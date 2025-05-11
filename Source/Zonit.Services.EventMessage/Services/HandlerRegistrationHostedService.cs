using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Zonit.Services.EventMessage.Services;

/// <summary>
/// Uniwersalna usługa do rejestracji handlerów
/// </summary>
public class HandlerRegistrationHostedService<THandler>(
    IServiceProvider serviceProvider,
    ILogger<HandlerRegistrationHostedService<THandler>> logger) : IHostedService where THandler : IHandler
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _handlerTypeName = typeof(THandler).Name;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting {HandlerType} registration", _handlerTypeName);

        try
        {
            // Używamy osobnego scope tylko do pobrania handlerów
            using var scope = _serviceProvider.CreateScope();
            var handlers = scope.ServiceProvider.GetServices<THandler>().ToList();

            _logger.LogInformation("Found {Count} {HandlerType} to register", handlers.Count, _handlerTypeName);

            // Rejestrujemy każdy handler, używając root service provider
            foreach (var handler in handlers)
            {
                try
                {
                    handler.Subscribe(_serviceProvider);
                    _logger.LogDebug("Registered {HandlerType}: {HandlerName}",
                        _handlerTypeName, handler.GetType().Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register {HandlerType}: {HandlerName}",
                        _handlerTypeName, handler.GetType().Name);
                }
            }

            _logger.LogInformation("{HandlerType} registration completed", _handlerTypeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register {HandlerType}", _handlerTypeName);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping {HandlerType} service", _handlerTypeName);
        return Task.CompletedTask;
    }
}
