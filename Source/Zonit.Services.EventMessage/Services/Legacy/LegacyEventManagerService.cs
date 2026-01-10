// Suppress obsolete warnings - this adapter intentionally uses legacy types
#pragma warning disable CS0618

namespace Zonit.Services.EventMessage.Services;

/// <summary>
/// Adapter: Legacy IEventManager ? New Zonit.Messaging.Events.IEventManager.
/// Pozwala na stopniow¹ migracjê bez przepisywania ca³ego kodu.
/// </summary>
internal sealed class LegacyEventManagerService : IEventManager
{
    private readonly Zonit.Messaging.Events.IEventManager _newManager;

    public LegacyEventManagerService(Zonit.Messaging.Events.IEventManager newManager)
    {
        _newManager = newManager;
    }

    public void Publish<TEvent>(TEvent payload) where TEvent : notnull
    {
        _newManager.Publish(payload);
    }

    public void Publish(string eventName, object payload)
    {
        _newManager.Publish(eventName, payload);
    }

    public void Subscribe<TEvent>(
        Func<PayloadModel<TEvent>, Task> handler,
        int workerCount = 10,
        TimeSpan? timeout = null) where TEvent : notnull
    {
        // Konwertuj legacy handler na nowy format
        _newManager.Subscribe<TEvent>(async (data, cancellationToken) =>
        {
            var legacyPayload = new PayloadModel<TEvent>
            {
                Data = data,
                CancellationToken = cancellationToken
            };
            await handler(legacyPayload);
        }, new Zonit.Messaging.Events.EventSubscriptionOptions
        {
            WorkerCount = workerCount,
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        });
    }

    public void Subscribe(
        string eventName,
        Func<PayloadModel<object>, Task> handler,
        int workerCount = 10,
        TimeSpan? timeout = null)
    {
        _newManager.Subscribe(eventName, async (data, cancellationToken) =>
        {
            var legacyPayload = new PayloadModel<object>
            {
                Data = data,
                CancellationToken = cancellationToken
            };
            await handler(legacyPayload);
        }, new Zonit.Messaging.Events.EventSubscriptionOptions
        {
            WorkerCount = workerCount,
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        });
    }

    public bool SupportsGenericSubscription() => true;

    public IEventTransaction CreateTransaction()
    {
        var newTransaction = _newManager.CreateTransaction();
        return new LegacyEventTransaction(newTransaction);
    }
}
