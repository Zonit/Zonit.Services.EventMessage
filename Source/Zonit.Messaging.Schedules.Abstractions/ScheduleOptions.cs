using Zonit.Extensions;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Options for configuring a schedule registration.
/// </summary>
public sealed class ScheduleOptions
{
    /// <summary>
    /// Schedule rules (when to execute).
    /// At least one schedule is required.
    /// </summary>
    public Schedule[] Schedules { get; set; } = [];

    /// <summary>
    /// Optional identifier for finding this schedule later (e.g., database record ID).
    /// Use this to associate a schedule with an external entity.
    /// </summary>
    public Guid? ExtensionId { get; set; }

    /// <summary>
    /// Whether to execute immediately when the application starts.
    /// Default: false.
    /// </summary>
    public bool ExecuteOnStartup { get; set; }

    /// <summary>
    /// Timeout for each execution.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of retry attempts on failure.
    /// Default: 0 (no retries).
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Delay between retry attempts.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to stop the schedule after MaxRetries consecutive failures.
    /// Default: false (continue trying on next scheduled time).
    /// </summary>
    public bool StopOnMaxRetries { get; set; }

    /// <summary>
    /// Optional name for this schedule (used in logs and monitoring).
    /// Default: derived from handler type name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional description for this schedule.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Time zone for calendar-based schedules.
    /// Default: Local time zone.
    /// </summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;
}

/// <summary>
/// Options for configuring a schedule with initial data.
/// </summary>
/// <typeparam name="TData">Type of schedule data.</typeparam>
public sealed class ScheduleOptions<TData> where TData : notnull
{
    /// <summary>
    /// Initial data for the schedule.
    /// </summary>
    public TData? Data { get; set; }

    /// <summary>
    /// Schedule rules (when to execute).
    /// At least one schedule is required.
    /// </summary>
    public Schedule[] Schedules { get; set; } = [];

    /// <summary>
    /// Optional identifier for finding this schedule later (e.g., database record ID).
    /// Use this to associate a schedule with an external entity.
    /// </summary>
    public Guid? ExtensionId { get; set; }

    /// <summary>
    /// Whether to execute immediately when the application starts.
    /// Default: false.
    /// </summary>
    public bool ExecuteOnStartup { get; set; }

    /// <summary>
    /// Timeout for each execution.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of retry attempts on failure.
    /// Default: 0 (no retries).
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Delay between retry attempts.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to stop the schedule after MaxRetries consecutive failures.
    /// Default: false (continue trying on next scheduled time).
    /// </summary>
    public bool StopOnMaxRetries { get; set; }

    /// <summary>
    /// Optional name for this schedule (used in logs and monitoring).
    /// Default: derived from handler type name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional description for this schedule.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Time zone for calendar-based schedules.
    /// Default: Local time zone.
    /// </summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;
}
