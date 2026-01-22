namespace Zonit.Messaging.Schedules;

/// <summary>
/// Represents an empty value for handlers that don't require data.
/// Used with <see cref="IScheduleHandler{TData}"/> when no data is needed.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The default (and only) value of Unit.
    /// </summary>
    public static readonly Unit Value = default;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
