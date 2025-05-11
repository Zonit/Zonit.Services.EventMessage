namespace Zonit.Services.EventMessage;

/// <summary>
/// Interfejs do tworzenia zadań
/// </summary>
public interface ITaskProvider
{
    void Publish(object payload, Guid? extensionId = null);
}