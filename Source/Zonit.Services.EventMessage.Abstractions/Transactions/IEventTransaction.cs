namespace Zonit.Services.EventMessage;

/// <summary>
/// Interfejs dla transakcji zdarze�, zapewniaj�cy sekwencyjne wykonywanie zdarze�
/// </summary>
public interface IEventTransaction : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Dodaje zdarzenie do transakcji
    /// </summary>
    /// <param name="payload">Dane zdarzenia</param>
    /// <param name="eventName">Opcjonalna nazwa zdarzenia</param>
    void Enqueue(object payload, string? eventName = null);

    /// <summary>
    /// Zatwierdza i przetwarza wszystkie zdarzenia w transakcji
    /// </summary>
    Task CommitAsync();

    /// <summary>
    /// Czeka na zako�czenie wszystkich operacji transakcji
    /// </summary>
    Task WaitForCompletionAsync();
}