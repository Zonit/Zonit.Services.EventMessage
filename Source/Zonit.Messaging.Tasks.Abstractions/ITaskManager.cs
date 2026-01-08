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
}
