namespace Zonit.Messaging.Tasks;

/// <summary>
/// Kontekst postêpu dostêpny w handlerze zadania.
/// Automatycznie oblicza p³ynny % na podstawie szacowanych czasów kroków.
/// </summary>
public interface ITaskProgressContext
{
    /// <summary>
    /// Przechodzi do nastêpnego kroku.
    /// System automatycznie oblicza % i wysy³a aktualizacje gdy zmieni siê o 1%.
    /// </summary>
    /// <param name="message">Opcjonalny komunikat zastêpuj¹cy domyœlny z definicji kroku.</param>
    Task NextAsync(string? message = null);

    /// <summary>
    /// Przeskakuje do konkretnego kroku (0-based index).
    /// </summary>
    /// <param name="stepIndex">Indeks kroku (0, 1, 2...).</param>
    /// <param name="message">Opcjonalny komunikat.</param>
    Task GoToAsync(int stepIndex, string? message = null);

    /// <summary>
    /// Aktualizuje tylko komunikat bez zmiany kroku.
    /// </summary>
    /// <param name="message">Komunikat do wyœwietlenia.</param>
    Task SetMessageAsync(string message);

    /// <summary>
    /// Wymusza konkretny % (dla tasków bez zdefiniowanych kroków).
    /// </summary>
    /// <param name="percentage">Wartoœæ 0-100.</param>
    /// <param name="message">Opcjonalny komunikat.</param>
    Task SetProgressAsync(int percentage, string? message = null);

    /// <summary>
    /// Aktualny indeks kroku (0-based).
    /// </summary>
    int CurrentStepIndex { get; }

    /// <summary>
    /// Aktualny obliczony % (0-100).
    /// </summary>
    int CurrentProgress { get; }

    /// <summary>
    /// Ca³kowita liczba zdefiniowanych kroków.
    /// </summary>
    int TotalSteps { get; }
}
