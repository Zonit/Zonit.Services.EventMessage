namespace Zonit.Services.EventMessage;

/// <summary>
/// Interfejs do komunikacji z systemem wiadomości
/// </summary>
public interface IMessageProvider
{
    /// <summary>
    /// Interfejs do komunikacji z systemem zdarzeń
    /// </summary>
    IEventProvider Event { get; }

    /// <summary>
    /// Interfejs do komunikacji z systemem poleceń
    /// </summary>
    ICommandProvider Command { get; }

    /// <summary>
    /// Interfejs do komunikacji z systemem harmonogramu
    /// </summary>
    ISchedulerProvider Scheduler { get; }

    ITaskProvider Task { get; }
}