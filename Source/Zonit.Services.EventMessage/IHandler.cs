using System.ComponentModel;

namespace Zonit.Services.EventMessage;

/// <summary>
/// [LEGACY] Wspólny interfejs bazowy dla wszystkich handlerów (zarówno eventów jak i zadań).
/// </summary>
/// <remarks>
/// <para><b>Ten interfejs jest przestarzały.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// Dla eventów użyj: <c>Zonit.Messaging.Events.IEventHandler&lt;TEvent&gt;</c><br/>
/// Dla zadań użyj: <c>Zonit.Messaging.Tasks.ITaskHandler&lt;TTask&gt;</c>
/// </para>
/// </remarks>
[Obsolete("Use Zonit.Messaging.Events.IEventHandler<T> or Zonit.Messaging.Tasks.ITaskHandler<T> instead.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IHandler
{
    /// <summary>
    /// Rejestruje handler w systemie.
    /// </summary>
    void Subscribe(IServiceProvider serviceProvider);
}