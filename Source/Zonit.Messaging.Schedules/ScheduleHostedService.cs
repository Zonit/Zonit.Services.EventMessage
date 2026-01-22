using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Configuration for a registered schedule.
/// </summary>
internal sealed class ScheduleRegistration
{
    public required Type HandlerType { get; init; }
    public required Schedule[] Schedules { get; init; }
    public string? Name { get; init; }
    public bool ExecuteOnStartup { get; init; }
}

/// <summary>
/// Hosted service that starts all registered schedules when the application starts.
/// </summary>
internal sealed class ScheduleHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduleHostedService> _logger;
    private readonly List<ScheduleId> _scheduleIds = [];

    public ScheduleHostedService(
        IServiceProvider serviceProvider,
        ILogger<ScheduleHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var registrations = _serviceProvider.GetServices<ScheduleRegistration>();
        var scheduleProvider = _serviceProvider.GetRequiredService<IScheduleProvider>();

        foreach (var registration in registrations)
        {
            try
            {
                var id = scheduleProvider.Start(
                    Unit.Value,
                    options =>
                    {
                        options.Name = registration.Name ?? registration.HandlerType.Name;
                        options.Schedules = registration.Schedules;
                        options.ExecuteOnStartup = registration.ExecuteOnStartup;
                    });

                _scheduleIds.Add(id);
                _logger.LogInformation(
                    "Started schedule '{Name}' with {Count} schedule rule(s)",
                    registration.Name ?? registration.HandlerType.Name,
                    registration.Schedules.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start schedule for handler {Handler}", registration.HandlerType.Name);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        var scheduleProvider = _serviceProvider.GetService<IScheduleProvider>();
        if (scheduleProvider is null)
            return Task.CompletedTask;

        foreach (var id in _scheduleIds)
        {
            try
            {
                scheduleProvider.Stop(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop schedule {Id}", id);
            }
        }

        _scheduleIds.Clear();
        return Task.CompletedTask;
    }
}
