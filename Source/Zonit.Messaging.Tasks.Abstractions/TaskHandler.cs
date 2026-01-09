namespace Zonit.Messaging.Tasks;

/// <summary>
/// Abstrakcyjna klasa bazowa dla handlerów zadañ z konfiguracj¹ i postêpem.
/// </summary>
/// <typeparam name="TData">Typ danych zadania.</typeparam>
public abstract class TaskHandler<TData> : ITaskHandler<TData> where TData : notnull
{
    /// <summary>
    /// Liczba równoleg³ych workerów przetwarzaj¹cych zadania tego typu.
    /// Domyœlnie 10.
    /// </summary>
    public virtual int WorkerCount => 10;

    /// <summary>
    /// Maksymalny czas wykonania pojedynczego zadania.
    /// Domyœlnie 5 minut.
    /// </summary>
    public virtual TimeSpan Timeout => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Definicja kroków postêpu z szacowanymi czasami.
    /// Null = brak œledzenia postêpu.
    /// </summary>
    public virtual TaskProgressStep[]? ProgressSteps => null;

    /// <summary>
    /// Metoda obs³ugi zadania - implementowana przez u¿ytkownika.
    /// </summary>
    /// <param name="data">Dane zadania.</param>
    /// <param name="progress">Kontekst do raportowania postêpu.</param>
    /// <param name="cancellationToken">Token anulowania.</param>
    protected abstract Task HandleAsync(
        TData data,
        ITaskProgressContext progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Implementacja interfejsu - wywo³ywana przez system.
    /// Nie wywo³uj tej metody bezpoœrednio.
    /// </summary>
    Task ITaskHandler<TData>.HandleAsync(TaskPayload<TData> payload)
    {
        if (payload.Progress is null)
        {
            throw new InvalidOperationException(
                "TaskHandler must be executed through TaskManager which provides ITaskProgressContext. " +
                "Do not call HandleAsync directly.");
        }

        return HandleAsync(payload.Data, payload.Progress, payload.CancellationToken);
    }
}
