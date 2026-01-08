namespace Zonit.Messaging.Tasks;

/// <summary>
/// Domyœlna implementacja ITaskProvider.
/// Wrapper nad ITaskManager dla publicznego API.
/// </summary>
public sealed class TaskProvider : ITaskProvider
{
    private readonly ITaskManager _taskManager;

    public TaskProvider(ITaskManager taskManager)
    {
        _taskManager = taskManager;
    }

    public void Publish<TTask>(TTask payload) where TTask : notnull
    {
        _taskManager.Publish(payload);
    }

    public void Publish<TTask>(TTask payload, Guid extensionId) where TTask : notnull
    {
        _taskManager.Publish(payload, extensionId);
    }
}
