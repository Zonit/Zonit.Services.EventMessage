namespace Zonit.Messaging.Tasks;

/// <summary>
/// Status wykonania zadania.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Zadanie oczekuje na przetworzenie.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Zadanie jest w trakcie przetwarzania.
    /// </summary>
    Processing,

    /// <summary>
    /// Zadanie zakoñczone pomyœlnie.
    /// </summary>
    Completed,

    /// <summary>
    /// Zadanie zakoñczone b³êdem.
    /// </summary>
    Failed,

    /// <summary>
    /// Zadanie anulowane.
    /// </summary>
    Cancelled
}
