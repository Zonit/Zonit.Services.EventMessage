namespace Zonit.Messaging.Tasks;

/// <summary>
/// Definicja kroku postêpu z szacowanym czasem trwania.
/// </summary>
/// <param name="EstimatedDuration">Szacowany czas trwania kroku (u¿ywany do obliczania p³ynnego %).</param>
/// <param name="Message">Opcjonalny komunikat wyœwietlany podczas tego kroku.</param>
public readonly record struct TaskProgressStep(
    TimeSpan EstimatedDuration,
    string? Message = null);
