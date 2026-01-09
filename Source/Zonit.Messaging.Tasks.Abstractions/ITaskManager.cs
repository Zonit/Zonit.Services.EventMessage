namespace Zonit.Messaging.Tasks;

/// <summary>
/// Manager zadañ w tle - odpowiada za subskrypcje i publikacjê.
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
    /// Publikuje zadanie z okreœlon¹ nazw¹.
    /// </summary>
    /// <param name="taskName">Nazwa zadania</param>
    /// <param name="payload">Dane zadania</param>
    /// <param name="extensionId">Opcjonalny identyfikator rozszerzenia</param>
    void Publish(string taskName, object payload, Guid? extensionId = null);

    /// <summary>
    /// Subskrybuje handler do zadañ.
    /// </summary>
    /// <typeparam name="TTask">Typ zadania</typeparam>
    /// <param name="handler">Funkcja obs³uguj¹ca zadanie</param>
    /// <param name="options">Opcje subskrypcji</param>
    void Subscribe<TTask>(Func<TaskPayload<TTask>, Task> handler, TaskSubscriptionOptions? options = null) 
        where TTask : notnull;

    /// <summary>
    /// Subskrybuje handler do zadañ z okreœlon¹ nazw¹.
    /// </summary>
    /// <param name="taskName">Nazwa zadania</param>
    /// <param name="handler">Funkcja obs³uguj¹ca zadanie</param>
    /// <param name="options">Opcje subskrypcji</param>
    void Subscribe(string taskName, Func<TaskPayload<object>, Task> handler, TaskSubscriptionOptions? options = null);

    /// <summary>
    /// Subskrybuje zmiany stanu wszystkich zadañ.
    /// </summary>
    /// <param name="handler">Handler wywo³ywany przy ka¿dej zmianie stanu.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange(Action<TaskState> handler);

    /// <summary>
    /// Subskrybuje zmiany stanu zadañ dla konkretnego ExtensionId.
    /// Bardziej wydajne ni¿ filtrowanie w handlerze.
    /// </summary>
    /// <param name="extensionId">Identyfikator rozszerzenia do œledzenia.</param>
    /// <param name="handler">Handler wywo³ywany przy ka¿dej zmianie stanu.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange(Guid extensionId, Action<TaskState> handler);

    /// <summary>
    /// Subskrybuje zmiany stanu zadañ okreœlonego typu.
    /// Umo¿liwia œledzenie konkretnych typów tasków (np. ArticleTask, ProductTask).
    /// </summary>
    /// <typeparam name="TTask">Typ zadania do œledzenia.</typeparam>
    /// <param name="handler">Handler wywo³ywany przy ka¿dej zmianie stanu z dostêpem do danych zadania.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange<TTask>(Action<TaskState<TTask>> handler) where TTask : notnull;

    /// <summary>
    /// Subskrybuje zmiany stanu zadañ okreœlonego typu dla konkretnego ExtensionId.
    /// </summary>
    /// <typeparam name="TTask">Typ zadania do œledzenia.</typeparam>
    /// <param name="extensionId">Identyfikator rozszerzenia do œledzenia.</param>
    /// <param name="handler">Handler wywo³ywany przy ka¿dej zmianie stanu z dostêpem do danych zadania.</param>
    /// <returns>Disposable do anulowania subskrypcji.</returns>
    IDisposable OnChange<TTask>(Guid extensionId, Action<TaskState<TTask>> handler) where TTask : notnull;

    /// <summary>
    /// Pobiera aktywne zadania (Pending lub Processing).
    /// </summary>
    /// <param name="extensionId">Opcjonalny filtr na ExtensionId.</param>
    /// <returns>Kolekcja aktywnych zadañ.</returns>
    IReadOnlyCollection<TaskState> GetActiveTasks(Guid? extensionId = null);

    /// <summary>
    /// Pobiera stan konkretnego zadania.
    /// </summary>
    /// <param name="taskId">Identyfikator zadania.</param>
    /// <returns>Stan zadania lub null jeœli nie istnieje.</returns>
    TaskState? GetTaskState(Guid taskId);
}
