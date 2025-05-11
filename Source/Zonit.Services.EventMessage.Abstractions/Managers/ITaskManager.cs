namespace Zonit.Services.EventMessage;

public interface ITaskManager
{
    /// <summary>
    /// Publishes a task for processing
    /// </summary>
    /// <param name="task">Task event model to process</param>
    void Publish(TaskEventModel task);

    /// <summary>
    /// Subscribes to perform tasks for a specific model type
    /// </summary>
    /// <typeparam name="TModel">Type of data model</typeparam>
    /// <param name="handler">Function that handles the task</param>
    /// <param name="workers">Number of parallel workers</param>
    /// <param name="timeout">Maximum time allowed for task execution</param>
    void Subscribe<TModel>(Func<PayloadModel<TModel>, Task> handler, int? workers = null, TimeSpan? timeout = null);

    /// <summary>
    /// Subscribes to task change events
    /// </summary>
    /// <param name="change">Action to perform when task status changes</param>
    void EventOnChange(Action<TaskEventModel> change);

    /// <summary>
    /// Gets all active tasks
    /// </summary>
    /// <param name="extensionId">Optional extension ID filter</param>
    /// <returns>Collection of active tasks</returns>
    IReadOnlyCollection<TaskEventModel> GetActiveTasks(Guid? extensionId = null);

    /// <summary>
    /// Gets all tasks for a specific entity (organization, project, user)
    /// </summary>
    /// <param name="entities">Entity filters to search by</param>
    /// <returns>Collection of tasks matching the criteria</returns>
    IReadOnlyCollection<TaskEventModel> GetTasksByEntities(EntitesModel entities);

    /// <summary>
    /// Unsubscribes from tasks of a specific model type
    /// </summary>
    /// <typeparam name="TModel">Type of model to unsubscribe from</typeparam>
    void Unsubscribe<TModel>();

    /// <summary>
    /// Removes all completed, failed, and cancelled tasks from the collection
    /// </summary>
    /// <returns>Number of tasks removed</returns>
    int CleanupCompletedTasks();
}