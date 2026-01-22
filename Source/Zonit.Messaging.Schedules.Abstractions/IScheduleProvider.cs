using Zonit.Extensions;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Provider for publishing and managing scheduled jobs.
/// </summary>
public interface IScheduleProvider
{
    /// <summary>
    /// Starts a new dynamic schedule with data and schedule rules.
    /// </summary>
    /// <typeparam name="TData">Type of schedule data.</typeparam>
    /// <param name="data">Data passed to the handler on each execution.</param>
    /// <param name="schedules">One or more schedule rules.</param>
    /// <returns>Unique identifier for the schedule.</returns>
    ScheduleId Start<TData>(TData data, params Schedule[] schedules) where TData : notnull;

    /// <summary>
    /// Starts a new dynamic schedule with an extension identifier.
    /// </summary>
    /// <typeparam name="TData">Type of schedule data.</typeparam>
    /// <param name="data">Data passed to the handler on each execution.</param>
    /// <param name="extensionId">Optional identifier for finding this schedule later (e.g., database record ID).</param>
    /// <param name="schedules">One or more schedule rules.</param>
    /// <returns>Unique identifier for the schedule.</returns>
    ScheduleId Start<TData>(TData data, Guid extensionId, params Schedule[] schedules) where TData : notnull;

    /// <summary>
    /// Starts a new dynamic schedule with full options.
    /// </summary>
    /// <typeparam name="TData">Type of schedule data.</typeparam>
    /// <param name="data">Data passed to the handler on each execution.</param>
    /// <param name="configure">Configuration action for schedule options.</param>
    /// <returns>Unique identifier for the schedule.</returns>
    ScheduleId Start<TData>(TData data, Action<ScheduleOptions> configure) where TData : notnull;

    /// <summary>
    /// Stops a running schedule.
    /// </summary>
    /// <param name="id">Schedule identifier.</param>
    /// <returns>True if the schedule was stopped; false if not found.</returns>
    bool Stop(ScheduleId id);

    /// <summary>
    /// Pauses a running schedule (can be resumed later).
    /// </summary>
    /// <param name="id">Schedule identifier.</param>
    /// <returns>True if the schedule was paused; false if not found.</returns>
    bool Pause(ScheduleId id);

    /// <summary>
    /// Resumes a paused schedule.
    /// </summary>
    /// <param name="id">Schedule identifier.</param>
    /// <returns>True if the schedule was resumed; false if not found.</returns>
    bool Resume(ScheduleId id);

    /// <summary>
    /// Triggers immediate execution of a schedule (outside of normal schedule).
    /// </summary>
    /// <param name="id">Schedule identifier.</param>
    /// <returns>True if execution was triggered; false if not found.</returns>
    bool TriggerNow(ScheduleId id);

    /// <summary>
    /// Gets the current state of a schedule.
    /// </summary>
    /// <param name="id">Schedule identifier.</param>
    /// <returns>Schedule state or null if not found.</returns>
    ScheduleState? GetState(ScheduleId id);

    /// <summary>
    /// Finds a schedule by its extension identifier.
    /// </summary>
    /// <param name="extensionId">The extension identifier to search for.</param>
    /// <returns>Schedule state or null if not found.</returns>
    ScheduleState? FindByExtensionId(Guid extensionId);

    /// <summary>
    /// Finds all schedules with the specified extension identifier.
    /// </summary>
    /// <param name="extensionId">The extension identifier to search for.</param>
    /// <returns>Collection of matching schedule states.</returns>
    IReadOnlyCollection<ScheduleState> FindAllByExtensionId(Guid extensionId);

    /// <summary>
    /// Gets all registered schedules.
    /// </summary>
    /// <returns>Collection of all schedule states.</returns>
    IReadOnlyCollection<ScheduleState> GetAllSchedules();

    /// <summary>
    /// Gets all active schedules (Running or Paused).
    /// </summary>
    /// <returns>Collection of active schedule states.</returns>
    IReadOnlyCollection<ScheduleState> GetActiveSchedules();

    /// <summary>
    /// Subscribes to schedule state changes.
    /// </summary>
    /// <param name="handler">Handler called on each state change.</param>
    /// <returns>Disposable to unsubscribe.</returns>
    IDisposable OnChange(Action<ScheduleState> handler);

    /// <summary>
    /// Subscribes to state changes for a specific schedule.
    /// </summary>
    /// <param name="id">Schedule identifier.</param>
    /// <param name="handler">Handler called on each state change.</param>
    /// <returns>Disposable to unsubscribe.</returns>
    IDisposable OnChange(ScheduleId id, Action<ScheduleState> handler);
}
