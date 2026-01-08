namespace Zonit.Messaging.Tasks;

/// <summary>
/// Handler zadañ w tle.
/// </summary>
/// <typeparam name="TTask">Typ zadania do obs³ugi</typeparam>
public interface ITaskHandler<TTask> where TTask : notnull
{
    /// <summary>
    /// Obs³uguje zadanie.
    /// </summary>
    /// <param name="payload">Dane zadania wraz z metadanymi</param>
    Task HandleAsync(TaskPayload<TTask> payload);
}
