namespace Zonit.Messaging.Schedules;

/// <summary>
/// Zero-size per-handler marker used as the data payload for <see cref="IScheduleHandler"/>
/// registrations made via <c>AddSchedule&lt;THandler&gt;</c>.
/// </summary>
/// <remarks>
/// Using a distinct marker type per handler gives each <c>AddSchedule</c> registration its own
/// <c>IScheduleHandler&lt;ScheduleMarker&lt;THandler&gt;&gt;</c> resolution slot, so schedules never
/// cross-trigger other handlers.
/// </remarks>
/// <typeparam name="THandler">Handler type associated with this marker.</typeparam>
public readonly struct ScheduleMarker<THandler> : IEquatable<ScheduleMarker<THandler>>
    where THandler : class, IScheduleHandler
{
    /// <summary>
    /// The default (and only) value.
    /// </summary>
    public static readonly ScheduleMarker<THandler> Value = default;

    /// <inheritdoc />
    public bool Equals(ScheduleMarker<THandler> other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ScheduleMarker<THandler>;

    /// <inheritdoc />
    public override int GetHashCode() => typeof(THandler).GetHashCode();

    /// <inheritdoc />
    public override string ToString() => $"ScheduleMarker<{typeof(THandler).Name}>";

    /// <summary>Equality operator.</summary>
    public static bool operator ==(ScheduleMarker<THandler> left, ScheduleMarker<THandler> right) => true;

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(ScheduleMarker<THandler> left, ScheduleMarker<THandler> right) => false;
}
