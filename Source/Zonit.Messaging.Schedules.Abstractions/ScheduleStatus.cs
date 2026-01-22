namespace Zonit.Messaging.Schedules;

/// <summary>
/// Status of a scheduled job.
/// </summary>
public enum ScheduleStatus
{
    /// <summary>
    /// Schedule is registered but not yet started.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Schedule is active and waiting for next execution.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Schedule is temporarily paused.
    /// </summary>
    Paused = 2,

    /// <summary>
    /// Schedule has been stopped and will not execute again.
    /// </summary>
    Stopped = 3,

    /// <summary>
    /// Schedule completed (reached MaxExecutions limit).
    /// </summary>
    Completed = 4,

    /// <summary>
    /// Schedule failed with an error.
    /// </summary>
    Failed = 5
}
