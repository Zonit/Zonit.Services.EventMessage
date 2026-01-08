// Suppress obsolete warnings - this adapter intentionally uses legacy types
#pragma warning disable CS0618

using System.Collections.Concurrent;

namespace Zonit.Services.EventMessage.Services;

/// <summary>
/// Adapter: Legacy ITaskManager ? New Zonit.Messaging.Tasks.ITaskManager.
/// Pozwala na stopniow¹ migracjê bez przepisywania ca³ego kodu.
/// </summary>
internal sealed class LegacyTaskManagerService : ITaskManager
{
    private readonly Zonit.Messaging.Tasks.ITaskManager _newManager;
    private readonly ConcurrentDictionary<Guid, TaskInfo> _activeTasks = new();
    private readonly List<Func<TaskEventArgs, Task>> _changeHandlers = [];

    public LegacyTaskManagerService(Zonit.Messaging.Tasks.ITaskManager newManager)
    {
        _newManager = newManager;
    }

    public void Publish<TTask>(TTask payload, Guid? extensionId = null) where TTask : notnull
    {
        _newManager.Publish(payload, extensionId);
    }

    public void Subscribe<TTask>(
        Func<PayloadModel<TTask>, Task> handler,
        int workerCount = 10,
        TimeSpan? timeout = null) where TTask : notnull
    {
        // Konwertuj legacy handler na nowy format
        _newManager.Subscribe<TTask>(async taskPayload =>
        {
            var taskId = taskPayload.TaskId;
            var taskInfo = new TaskInfo { Id = taskId, Status = "Running" };
            _activeTasks[taskId] = taskInfo;

            // Notify change handlers
            await NotifyChangeAsync(new TaskEventArgs
            {
                Id = taskId,
                Status = "Running",
                Payload = new PayloadModel<object> { Data = taskPayload.Data! }
            });

            try
            {
                var legacyPayload = new PayloadModel<TTask>
                {
                    Data = taskPayload.Data,
                    CancellationToken = taskPayload.CancellationToken
                };
                await handler(legacyPayload);

                taskInfo = taskInfo with { Status = "Completed" };
                _activeTasks[taskId] = taskInfo;

                await NotifyChangeAsync(new TaskEventArgs
                {
                    Id = taskId,
                    Status = "Completed",
                    Payload = new PayloadModel<object> { Data = taskPayload.Data! }
                });
            }
            catch (Exception)
            {
                taskInfo = taskInfo with { Status = "Failed" };
                _activeTasks[taskId] = taskInfo;

                await NotifyChangeAsync(new TaskEventArgs
                {
                    Id = taskId,
                    Status = "Failed",
                    Payload = new PayloadModel<object> { Data = taskPayload.Data! }
                });
                throw;
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
            }
        }, new Zonit.Messaging.Tasks.TaskSubscriptionOptions
        {
            WorkerCount = workerCount,
            Timeout = timeout ?? TimeSpan.FromMinutes(5)
        });
    }

    public void EventOnChange(Func<TaskEventArgs, Task> handler)
    {
        _changeHandlers.Add(handler);
    }

    public IEnumerable<TaskInfo> GetActiveTasks()
    {
        return _activeTasks.Values.ToList();
    }

    private async Task NotifyChangeAsync(TaskEventArgs args)
    {
        foreach (var handler in _changeHandlers)
        {
            try
            {
                await handler(args);
            }
            catch
            {
                // Ignore handler errors
            }
        }
    }
}
