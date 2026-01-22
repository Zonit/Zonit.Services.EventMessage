namespace Zonit.Messaging.Schedules;

/// <summary>
/// Base interface for schedule handlers without data.
/// Implement this interface for simple recurring tasks that don't require parameters.
/// </summary>
/// <example>
/// <code>
/// public class CleanupHandler : IScheduleHandler
/// {
///     public async Task HandleAsync(CancellationToken cancellationToken)
///     {
///         // Perform cleanup...
///     }
/// }
/// 
/// // Register with schedule
/// services.AddSchedule&lt;CleanupHandler&gt;(Schedule.EveryMinutes(10));
/// </code>
/// </example>
public interface IScheduleHandler
{
    /// <summary>
    /// Handles the scheduled job execution.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for this execution.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task HandleAsync(CancellationToken cancellationToken);
}
