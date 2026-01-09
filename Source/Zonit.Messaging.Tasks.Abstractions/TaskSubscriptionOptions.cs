namespace Zonit.Messaging.Tasks;

/// <summary>
/// Opcje subskrypcji zadania.
/// </summary>
public sealed class TaskSubscriptionOptions
{
    /// <summary>
    /// Liczba równoleg³ych workerów przetwarzaj¹cych zadania.
    /// </summary>
    public int WorkerCount { get; set; } = 10;

    /// <summary>
    /// Maksymalny czas wykonania pojedynczego zadania.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Czy kontynuowaæ przetwarzanie po b³êdzie.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Maksymalna liczba prób ponowienia zadania.
    /// </summary>
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// OpóŸnienie miêdzy próbami ponowienia.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Definicja kroków postêpu dla zadania.
    /// Null = brak œledzenia postêpu.
    /// </summary>
    public TaskProgressStep[]? ProgressSteps { get; set; }
}
