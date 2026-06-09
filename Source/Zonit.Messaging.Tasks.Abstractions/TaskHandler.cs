namespace Zonit.Messaging.Tasks;

/// <summary>
/// Abstrakcyjna klasa bazowa dla handler�w zada� z konfiguracj� i post�pem.
/// </summary>
/// <typeparam name="TData">Typ danych zadania.</typeparam>
public abstract class TaskHandler<TData> : ITaskHandler<TData> where TData : notnull
{
    /// <summary>
    /// Liczba r�wnoleg�ych worker�w przetwarzaj�cych zadania tego typu.
    /// Domy�lnie 10.
    /// </summary>
    public virtual int WorkerCount => 10;

    /// <summary>
    /// Maksymalny czas wykonania pojedynczego zadania.
    /// Domy�lnie 5 minut. Ustaw <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> aby wy��czy�.
    /// </summary>
    public virtual TimeSpan Timeout => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maksymalna liczba ponowie� po b��dzie (rozdzielonych <see cref="RetryDelay"/>).
    /// Domy�lnie 0 (bez ponowie�).
    /// </summary>
    public virtual int MaxRetries => 0;

    /// <summary>
    /// Op�nienie mi�dzy ponowieniami. Domy�lnie 5 sekund.
    /// </summary>
    public virtual TimeSpan RetryDelay => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Czy kontynuowa� drenowanie kolejki po ostatecznym b��dzie zadania. Domy�lnie true.
    /// </summary>
    public virtual bool ContinueOnError => true;

    /// <summary>
    /// Definicja krok�w post�pu z szacowanymi czasami.
    /// Null = brak �ledzenia post�pu.
    /// </summary>
    public virtual TaskProgressStep[]? ProgressSteps => null;

    /// <summary>
    /// Tytu� zadania wy�wietlany w interfejsie u�ytkownika.
    /// Null = u�ywana b�dzie nazwa typu zadania.
    /// </summary>
    public virtual string? Title => null;

    /// <summary>
    /// Opis zadania wy�wietlany w interfejsie u�ytkownika.
    /// Null = brak opisu.
    /// </summary>
    public virtual string? Description => null;

    /// <summary>
    /// Metoda obs�ugi zadania - implementowana przez u�ytkownika.
    /// </summary>
    /// <param name="data">Dane zadania.</param>
    /// <param name="progress">Kontekst do raportowania post�pu.</param>
    /// <param name="cancellationToken">Token anulowania.</param>
    protected abstract Task HandleAsync(
        TData data,
        ITaskProgressContext progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Implementacja interfejsu - wywo�ywana przez system.
    /// Nie wywo�uj tej metody bezpo�rednio.
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
