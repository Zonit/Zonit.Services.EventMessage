using System.ComponentModel;

namespace Zonit.Services.EventMessage;

/// <summary>
/// [LEGACY] Provider do publikowania zadañ w tle.
/// </summary>
/// <remarks>
/// <para><b>Ten interfejs jest przestarza³y.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// Zamiast: <c>using Zonit.Services.EventMessage;</c><br/>
/// U¿yj: <c>using Zonit.Messaging.Tasks;</c><br/>
/// <br/>
/// Zamiast: <c>ITaskProvider</c> z namespace <c>Zonit.Services.EventMessage</c><br/>
/// U¿yj: <c>ITaskProvider</c> z namespace <c>Zonit.Messaging.Tasks</c>
/// </para>
/// </remarks>
[Obsolete("Use Zonit.Messaging.Tasks.ITaskProvider instead. Change namespace from 'Zonit.Services.EventMessage' to 'Zonit.Messaging.Tasks'.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITaskProvider
{
    /// <summary>
    /// Publikuje zadanie do wykonania.
    /// </summary>
    void Publish<TTask>(TTask payload) where TTask : notnull;

    /// <summary>
    /// Publikuje zadanie z okreœlonym identyfikatorem rozszerzenia.
    /// </summary>
    void Publish<TTask>(TTask payload, Guid extensionId) where TTask : notnull;
}
