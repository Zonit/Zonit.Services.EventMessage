namespace Zonit.Messaging.Tasks;

/// <summary>
/// Opcje subskrypcji zadania.
/// </summary>
public sealed class TaskSubscriptionOptions
{
    /// <summary>
    /// Liczba r�wnoleg�ych worker�w przetwarzaj�cych zadania.
    /// </summary>
    public int WorkerCount { get; set; } = 10;

    /// <summary>
    /// Maksymalny czas wykonania pojedynczego zadania.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Czy kontynuowa� przetwarzanie po b��dzie.
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Maksymalna liczba pr�b ponowienia zadania.
    /// </summary>
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// Op�nienie mi�dzy pr�bami ponowienia.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Definicja krok�w post�pu dla zadania.
    /// Null = brak �ledzenia post�pu.
    /// </summary>
    public TaskProgressStep[]? ProgressSteps { get; set; }

    /// <summary>
    /// Tytu� zadania wy�wietlany w interfejsie u�ytkownika.
    /// Null = u�ywana b�dzie nazwa typu zadania.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Opis zadania wy�wietlany w interfejsie u�ytkownika.
    /// Null = brak opisu.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Maksymalna liczba zbuforowanych zada� tego typu. <c>null</c> = bez limitu (domy�lnie).
    /// Ustaw limit, aby ograniczy� pami�� przy zalewie publikacji; po przekroczeniu limitu
    /// nadmiarowe zadania s� odrzucane z ostrze�eniem w logu (publikacja jest synchroniczna).
    /// </summary>
    public int? Capacity { get; set; }
}
