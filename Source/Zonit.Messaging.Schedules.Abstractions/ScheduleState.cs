using Zonit.Extensions;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Represents the current state of a scheduled job.
/// </summary>
public sealed class ScheduleState
{
    /// <summary>
    /// Unique identifier for this schedule.
    /// </summary>
    public required ScheduleId Id { get; init; }

    /// <summary>
    /// Optional identifier for finding this schedule (e.g., database record ID).
    /// </summary>
    public Guid? ExtensionId { get; init; }

    /// <summary>
    /// Name of the schedule (derived from handler type or custom name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Current status of the schedule.
    /// </summary>
    public required ScheduleStatus Status { get; set; }

    /// <summary>
    /// Schedule rules for this job.
    /// </summary>
    public required Schedule[] Schedules { get; init; }

    /// <summary>
    /// When the schedule was created/registered.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the last execution occurred (null if never executed).
    /// </summary>
    public DateTimeOffset? LastExecutionAt { get; set; }

    /// <summary>
    /// When the next execution is scheduled (null if not scheduled).
    /// </summary>
    public DateTimeOffset? NextExecutionAt { get; set; }

    /// <summary>
    /// Total number of executions so far.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Last error message that occurred during execution (null if no error).
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Duration of the last execution.
    /// </summary>
    public TimeSpan? LastExecutionDuration { get; set; }

    /// <summary>
    /// Optional description of the schedule.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Typed schedule state with access to the data payload.
/// </summary>
/// <typeparam name="TData">Type of schedule data.</typeparam>
public sealed class ScheduleState<TData> where TData : notnull
{
    /// <summary>
    /// The base schedule state.
    /// </summary>
    public required ScheduleState State { get; init; }

    /// <summary>
    /// The data payload for this schedule.
    /// </summary>
    public required TData Data { get; init; }
}
