namespace Zonit.Messaging.Schedules;

/// <summary>
/// Unique identifier for a running schedule.
/// </summary>
public readonly record struct ScheduleId(Guid Value)
{
    /// <summary>
    /// Creates a new unique schedule identifier.
    /// </summary>
    public static ScheduleId New() => new(Guid.NewGuid());

    /// <summary>
    /// Empty schedule identifier.
    /// </summary>
    public static readonly ScheduleId Empty = default;

    /// <summary>
    /// Returns true if this identifier has a value.
    /// </summary>
    public bool HasValue => Value != Guid.Empty;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Implicit conversion to Guid.
    /// </summary>
    public static implicit operator Guid(ScheduleId id) => id.Value;

    /// <summary>
    /// Implicit conversion from Guid.
    /// </summary>
    public static implicit operator ScheduleId(Guid value) => new(value);
}
