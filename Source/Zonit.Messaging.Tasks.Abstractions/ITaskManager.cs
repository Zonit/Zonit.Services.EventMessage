namespace Zonit.Messaging.Tasks;

/// <summary>
/// Manager zada� w tle - odpowiada za subskrypcje i publikacj�.
/// </summary>
public interface ITaskManager
{
    /// <summary>
    /// Publikuje zadanie.
    /// </summary>
    /// <typeparam name="TTask">Typ zadania</typeparam>
    /// <param name="payload">Dane zadania</param>
    /// <param name="extensionId">Opcjonalny identyfikator rozszerzenia</param>
    void Publish<TTask>(TTask payload, Guid? extensionId = null) where TTask : notnull;

    /// <summary>
    /// Publikuje zadanie z okre�lon� nazw�.
    /// </summary>
    /// <param name="taskName">Nazwa zadania</param>
    /// <param name="payload">Dane zadania</param>
    /// <param name="extensionId">Opcjonalny identyfikator rozszerzenia</param>
    void Publish(string taskName, object payload, Guid? extensionId = null);

    /// <summary>
    /// Subskrybuje handler do zada�.
    /// </summary>
    /// <typeparam name="TTask">Typ zadania</typeparam>
    /// <param name="handler">Funkcja obs�uguj�ca zadanie</param>
    /// <param name="options">Opcje subskrypcji</param>
    void Subscribe<TTask>(Func<TaskPayload<TTask>, Task> handler, TaskSubscriptionOptions? options = null)
        where TTask : notnull;

    /// <summary>
    /// Subskrybuje handler do zada� z okre�lon� nazw�.
    /// </summary>
    /// <param name="taskName">Nazwa zadania</param>
    /// <param name="handler">Funkcja obs�uguj�ca zadanie</param>
    /// <param name="options">Opcje subskrypcji</param>
    void Subscribe(string taskName, Func<TaskPayload<object>, Task> handler, TaskSubscriptionOptions? options = null);

    /// <summary>
    /// Subskrybuje zmiany stanu wszystkich zada�.
    /// </summary>
    /// <param name="handler">Handler wywo�ywany przy ka�dej zmianie stanu.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange(Action<TaskState> handler);

    /// <summary>
    /// Subskrybuje zmiany stanu zada� dla konkretnego ExtensionId.
    /// Bardziej wydajne ni� filtrowanie w handlerze.
    /// </summary>
    /// <param name="extensionId">Identyfikator rozszerzenia do �ledzenia.</param>
    /// <param name="handler">Handler wywo�ywany przy ka�dej zmianie stanu.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange(Guid extensionId, Action<TaskState> handler);

    /// <summary>
    /// Subskrybuje zmiany stanu zada� okre�lonego typu.
    /// Umo�liwia �ledzenie konkretnych typ�w task�w (np. ArticleTask, ProductTask).
    /// </summary>
    /// <typeparam name="TTask">Typ zadania do �ledzenia.</typeparam>
    /// <param name="handler">Handler wywo�ywany przy ka�dej zmianie stanu z dost�pem do danych zadania.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange<TTask>(Action<TaskState<TTask>> handler) where TTask : notnull;

    /// <summary>
    /// Subskrybuje zmiany stanu zadań określonego typu dla konkretnego ExtensionId.
    /// </summary>
    /// <typeparam name="TTask">Typ zadania do śledzenia.</typeparam>
    /// <param name="extensionId">Identyfikator rozszerzenia do śledzenia.</param>
    /// <param name="handler">Handler wywoływany przy każdej zmianie stanu z dostępem do danych zadania.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange<TTask>(Guid extensionId, Action<TaskState<TTask>> handler) where TTask : notnull;

    /// <summary>
    /// Subskrybuje zmiany stanu zadań dwóch określonych typów.
    /// </summary>
    /// <typeparam name="TTask1">Pierwszy typ zadania do śledzenia.</typeparam>
    /// <typeparam name="TTask2">Drugi typ zadania do śledzenia.</typeparam>
    /// <param name="handler">Handler wywoływany przy każdej zmianie stanu.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange<TTask1, TTask2>(Action<TaskState> handler)
        where TTask1 : notnull
        where TTask2 : notnull;

    /// <summary>
    /// Subskrybuje zmiany stanu zadań trzech określonych typów.
    /// </summary>
    /// <typeparam name="TTask1">Pierwszy typ zadania do śledzenia.</typeparam>
    /// <typeparam name="TTask2">Drugi typ zadania do śledzenia.</typeparam>
    /// <typeparam name="TTask3">Trzeci typ zadania do śledzenia.</typeparam>
    /// <param name="handler">Handler wywoływany przy każdej zmianie stanu.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange<TTask1, TTask2, TTask3>(Action<TaskState> handler)
        where TTask1 : notnull
        where TTask2 : notnull
        where TTask3 : notnull;

    /// <summary>
    /// Subskrybuje zmiany stanu zadań czterech określonych typów.
    /// </summary>
    /// <typeparam name="TTask1">Pierwszy typ zadania do śledzenia.</typeparam>
    /// <typeparam name="TTask2">Drugi typ zadania do śledzenia.</typeparam>
    /// <typeparam name="TTask3">Trzeci typ zadania do śledzenia.</typeparam>
    /// <typeparam name="TTask4">Czwarty typ zadania do śledzenia.</typeparam>
    /// <param name="handler">Handler wywoływany przy każdej zmianie stanu.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange<TTask1, TTask2, TTask3, TTask4>(Action<TaskState> handler)
        where TTask1 : notnull
        where TTask2 : notnull
        where TTask3 : notnull
        where TTask4 : notnull;

    /// <summary>
    /// Pobiera aktywne zadania (Pending lub Processing).
    /// </summary>
    /// <param name="extensionId">Opcjonalny filtr na ExtensionId.</param>
    /// <returns>Kolekcja aktywnych zadań.</returns>
    IReadOnlyCollection<TaskState> GetActiveTasks(Guid? extensionId = null);

    /// <summary>
    /// Pobiera aktywne zadania określonego typu (Pending lub Processing).
    /// </summary>
    /// <typeparam name="TTask">Typ zadania do filtrowania.</typeparam>
    /// <param name="extensionId">Opcjonalny filtr na ExtensionId.</param>
    /// <returns>Kolekcja aktywnych zadań z typowanymi danymi.</returns>
    IReadOnlyCollection<TaskState<TTask>> GetActiveTasks<TTask>(Guid? extensionId = null) where TTask : notnull;

    /// <summary>
    /// Pobiera aktywne zadania dwóch określonych typów (Pending lub Processing).
    /// </summary>
    /// <typeparam name="TTask1">Pierwszy typ zadania do filtrowania.</typeparam>
    /// <typeparam name="TTask2">Drugi typ zadania do filtrowania.</typeparam>
    /// <param name="extensionId">Opcjonalny filtr na ExtensionId.</param>
    /// <returns>Kolekcja aktywnych zadań.</returns>
    IReadOnlyCollection<TaskState> GetActiveTasks<TTask1, TTask2>(Guid? extensionId = null)
        where TTask1 : notnull
        where TTask2 : notnull;

    /// <summary>
    /// Pobiera aktywne zadania trzech określonych typów (Pending lub Processing).
    /// </summary>
    /// <typeparam name="TTask1">Pierwszy typ zadania do filtrowania.</typeparam>
    /// <typeparam name="TTask2">Drugi typ zadania do filtrowania.</typeparam>
    /// <typeparam name="TTask3">Trzeci typ zadania do filtrowania.</typeparam>
    /// <param name="extensionId">Opcjonalny filtr na ExtensionId.</param>
    /// <returns>Kolekcja aktywnych zadań.</returns>
    IReadOnlyCollection<TaskState> GetActiveTasks<TTask1, TTask2, TTask3>(Guid? extensionId = null)
        where TTask1 : notnull
        where TTask2 : notnull
        where TTask3 : notnull;

    /// <summary>
    /// Pobiera aktywne zadania czterech określonych typów (Pending lub Processing).
    /// </summary>
    /// <typeparam name="TTask1">Pierwszy typ zadania do filtrowania.</typeparam>
    /// <typeparam name="TTask2">Drugi typ zadania do filtrowania.</typeparam>
    /// <typeparam name="TTask3">Trzeci typ zadania do filtrowania.</typeparam>
    /// <typeparam name="TTask4">Czwarty typ zadania do filtrowania.</typeparam>
    /// <param name="extensionId">Opcjonalny filtr na ExtensionId.</param>
    /// <returns>Kolekcja aktywnych zadań.</returns>
    IReadOnlyCollection<TaskState> GetActiveTasks<TTask1, TTask2, TTask3, TTask4>(Guid? extensionId = null)
        where TTask1 : notnull
        where TTask2 : notnull
        where TTask3 : notnull
        where TTask4 : notnull;

    /// <summary>
    /// Pobiera stan konkretnego zadania.
    /// </summary>
    /// <param name="taskId">Identyfikator zadania.</param>
    /// <returns>Stan zadania lub null je�li nie istnieje.</returns>
    TaskState? GetTaskState(Guid taskId);
}
