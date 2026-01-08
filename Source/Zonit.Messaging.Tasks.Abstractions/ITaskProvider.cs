namespace Zonit.Messaging.Tasks;

/// <summary>
/// Provider do publikowania zadañ w tle.
/// </summary>
public interface ITaskProvider
{
    /// <summary>
    /// Publikuje zadanie do wykonania.
    /// </summary>
    /// <typeparam name="TTask">Typ zadania</typeparam>
    /// <param name="payload">Dane zadania</param>
    void Publish<TTask>(TTask payload) where TTask : notnull;

    /// <summary>
    /// Publikuje zadanie z okreœlonym identyfikatorem rozszerzenia.
    /// </summary>
    /// <typeparam name="TTask">Typ zadania</typeparam>
    /// <param name="payload">Dane zadania</param>
    /// <param name="extensionId">Identyfikator rozszerzenia</param>
    void Publish<TTask>(TTask payload, Guid extensionId) where TTask : notnull;
}
