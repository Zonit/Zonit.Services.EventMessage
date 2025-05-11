namespace Zonit.Services.EventMessage.Services;

internal class TaskProviderService(ITaskManager taskManagerService) : ITaskProvider
{
    public void Publish(object payload, Guid? extensionId = null, EntitesModel? credential = null)
    {
        var taskEvent = new TaskEventModel
        {
            ExtensionId = extensionId,
            Payload = new PayloadModel
            {
                Data = payload,
                CancellationToken = CancellationToken.None
            },
            Entites = credential ?? new EntitesModel()
        };

        taskManagerService.Publish(taskEvent);
    }
}
