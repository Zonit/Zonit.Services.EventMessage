namespace Zonit.Messaging.Events;

/// <summary>
/// Abstract base class for event handlers with per-handler subscription configuration.
/// Inherit from this class instead of implementing <see cref="IEventHandler{TEvent}"/> directly
/// when you need to override defaults like <see cref="Timeout"/> or <see cref="WorkerCount"/>.
/// </summary>
/// <remarks>
/// <para>
/// Properties declared here are read once during application startup by
/// <c>EventHandlerRegistrationHostedService</c> when the corresponding subscription is wired up.
/// Subsequent changes have no effect on the active subscription.
/// </para>
/// <para>
/// Explicit options passed to <c>AddEvent&lt;THandler, TEvent&gt;(opts =&gt; ...)</c> always win
/// over handler-level overrides. If neither is provided, defaults from
/// <see cref="EventSubscriptionOptions"/> apply.
/// </para>
/// <example>
/// <code>
/// public class LongRunningEventHandler : EventHandler&lt;OrderPlacedEvent&gt;
/// {
///     public override TimeSpan Timeout => TimeSpan.FromMinutes(10);
///     public override int WorkerCount => 4;
///
///     protected override Task HandleAsync(OrderPlacedEvent data, CancellationToken cancellationToken)
///     {
///         // ...
///     }
/// }
/// </code>
/// </example>
/// </remarks>
/// <typeparam name="TEvent">Type of event handled.</typeparam>
public abstract class EventHandler<TEvent> : IEventHandler<TEvent> where TEvent : notnull
{
    /// <summary>
    /// Number of parallel workers processing events of this type.
    /// Default: 10.
    /// </summary>
    public virtual int WorkerCount => 10;

    /// <summary>
    /// Maximum execution time for a single event handler invocation.
    /// Default: 5 minutes.
    /// </summary>
    /// <remarks>
    /// Override to a smaller value for fast-path handlers (e.g. UI notifications) or
    /// a larger value for long-running handlers (e.g. data import, ML inference).
    /// </remarks>
    public virtual TimeSpan Timeout => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to continue processing the channel after a handler exception.
    /// Default: true (logs error and keeps draining the queue).
    /// </summary>
    public virtual bool ContinueOnError => true;

    /// <summary>
    /// Handles the event. Override to provide handler logic.
    /// </summary>
    /// <param name="data">Event payload.</param>
    /// <param name="cancellationToken">Cancellation token (linked with <see cref="Timeout"/>).</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    protected abstract Task HandleAsync(TEvent data, CancellationToken cancellationToken);

    /// <summary>
    /// Explicit interface implementation - invoked by the messaging infrastructure.
    /// Do not call directly; use the protected <see cref="HandleAsync(TEvent, CancellationToken)"/>
    /// override instead.
    /// </summary>
    Task IEventHandler<TEvent>.HandleAsync(TEvent data, CancellationToken cancellationToken)
        => HandleAsync(data, cancellationToken);
}
