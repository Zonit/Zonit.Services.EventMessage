using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Base registration for a startup schedule captured by <c>AddSchedule&lt;THandler&gt;</c>.
/// </summary>
internal abstract class ScheduleRegistration
{
    public required Schedule[] Schedules { get; init; }
    public string? Name { get; init; }
    public bool ExecuteOnStartup { get; init; }
    public TimeSpan? Timeout { get; init; }
    public int MaxRetries { get; init; }
    public TimeSpan? RetryDelay { get; init; }
    public bool StopOnMaxRetries { get; init; }
    public string? Description { get; init; }
    public TimeZoneInfo? TimeZone { get; init; }

    public abstract Type HandlerType { get; }

    /// <summary>
    /// Starts this schedule against the provided <see cref="IScheduleProvider"/>.
    /// </summary>
    public abstract ScheduleId Start(IScheduleProvider provider);

    protected void ApplyTo(ScheduleOptions options)
    {
        options.Schedules = Schedules;
        options.Name = Name ?? HandlerType.Name;
        options.ExecuteOnStartup = ExecuteOnStartup;
        options.MaxRetries = MaxRetries;
        options.StopOnMaxRetries = StopOnMaxRetries;
        options.Description = Description;
        if (Timeout.HasValue) options.Timeout = Timeout.Value;
        if (RetryDelay.HasValue) options.RetryDelay = RetryDelay.Value;
        if (TimeZone is not null) options.TimeZone = TimeZone;
    }
}

/// <summary>
/// Typed registration - preserves <typeparamref name="THandler"/> so we can start the schedule
/// with a unique <see cref="ScheduleMarker{THandler}"/> data type (no reflection, AOT-safe).
/// </summary>
internal sealed class ScheduleRegistration<THandler> : ScheduleRegistration
    where THandler : class, IScheduleHandler
{
    public override Type HandlerType => typeof(THandler);

    public override ScheduleId Start(IScheduleProvider provider)
    {
        return provider.Start(ScheduleMarker<THandler>.Value, ApplyTo);
    }
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
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var id = registration.Start(scheduleProvider);
                _scheduleIds.Add(id);
                _logger.LogInformation(
                    "Started schedule '{Name}' for {Handler} with {Count} schedule rule(s)",
                    registration.Name ?? registration.HandlerType.Name,
                    registration.HandlerType.Name,
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
