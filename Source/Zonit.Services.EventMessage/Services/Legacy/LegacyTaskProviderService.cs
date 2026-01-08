// Suppress obsolete warnings - this adapter intentionally uses legacy types
#pragma warning disable CS0618

namespace Zonit.Services.EventMessage.Services;

/// <summary>
/// Adapter: Legacy ITaskProvider ? New Zonit.Messaging.Tasks.ITaskProvider.
/// Pozwala na stopniow¹ migracjê bez przepisywania ca³ego kodu.
/// </summary>
internal sealed class LegacyTaskProviderService : ITaskProvider
{
    private readonly Zonit.Messaging.Tasks.ITaskProvider _newProvider;

    public LegacyTaskProviderService(Zonit.Messaging.Tasks.ITaskProvider newProvider)
    {
        _newProvider = newProvider;
    }

    public void Publish<TTask>(TTask payload) where TTask : notnull
    {
        _newProvider.Publish(payload);
    }

    public void Publish<TTask>(TTask payload, Guid extensionId) where TTask : notnull
    {
        _newProvider.Publish(payload, extensionId);
    }
}
