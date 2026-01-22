namespace Zonit.Messaging.Schedules;

/// <summary>
/// Handler for scheduled jobs.
/// Implement this interface to create a scheduled job handler.
/// </summary>
/// <typeparam name="TData">Type of data passed to the handler.</typeparam>
public interface IScheduleHandler<TData> where TData : notnull
{
    /// <summary>
    /// Handles the scheduled job execution.
    /// </summary>
    /// <param name="data">The data payload for this schedule.</param>
    /// <param name="cancellationToken">Cancellation token for this execution.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task HandleAsync(TData data, CancellationToken cancellationToken);
}
