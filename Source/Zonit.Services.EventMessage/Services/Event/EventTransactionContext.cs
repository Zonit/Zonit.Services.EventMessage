using System.Threading;

namespace Zonit.Services.EventMessage.Services.Event;

/// <summary>
/// Przechowuje kontekst aktualnej transakcji zdarze�
/// </summary>
internal static class EventTransactionContext
{
    private static readonly AsyncLocal<EventTransaction?> _currentTransaction = new();

    /// <summary>
    /// Ustawia bie��c� transakcj� dla aktualnego kontekstu asynchronicznego
    /// </summary>
    public static void SetCurrent(EventTransaction? transaction)
    {
        _currentTransaction.Value = transaction;
    }

    /// <summary>
    /// Pobiera bie��c� transakcj� dla aktualnego kontekstu asynchronicznego
    /// </summary>
    public static EventTransaction? GetCurrent() => _currentTransaction.Value;

    /// <summary>
    /// Sprawdza, czy istnieje aktywna transakcja w bie��cym kontek�cie
    /// </summary>
    public static bool HasActiveTransaction => _currentTransaction.Value != null;
}