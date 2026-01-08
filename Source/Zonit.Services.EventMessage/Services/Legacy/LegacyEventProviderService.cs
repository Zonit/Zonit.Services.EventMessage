// Suppress obsolete warnings - this adapter intentionally uses legacy types
#pragma warning disable CS0618

namespace Zonit.Services.EventMessage.Services;

/// <summary>
/// Adapter: Legacy IEventProvider ? New Zonit.Messaging.Events.IEventProvider.
/// Pozwala na stopniow¹ migracjê bez przepisywania ca³ego kodu.
/// </summary>
internal sealed class LegacyEventProviderService : IEventProvider
{
    private readonly Zonit.Messaging.Events.IEventProvider _newProvider;
    private readonly Zonit.Messaging.Events.IEventManager _newManager;

    public LegacyEventProviderService(
        Zonit.Messaging.Events.IEventProvider newProvider,
        Zonit.Messaging.Events.IEventManager newManager)
    {
        _newProvider = newProvider;
        _newManager = newManager;
    }

    public void Publish<TEvent>(TEvent payload) where TEvent : notnull
    {
        _newProvider.Publish(payload);
    }

    public void Publish(string eventName, object payload)
    {
        _newProvider.Publish(eventName, payload);
    }

    public IEventTransaction Transaction()
    {
        var newTransaction = _newManager.CreateTransaction();
        return new LegacyEventTransaction(newTransaction);
    }
}

/// <summary>
/// Adapter: Legacy IEventTransaction ? New Zonit.Messaging.Events.IEventTransaction.
/// </summary>
#pragma warning disable CS0618
internal sealed class LegacyEventTransaction : IEventTransaction
#pragma warning restore CS0618
{
    private readonly Zonit.Messaging.Events.IEventTransaction _newTransaction;

    public LegacyEventTransaction(Zonit.Messaging.Events.IEventTransaction newTransaction)
    {
        _newTransaction = newTransaction;
    }

    public void Enqueue<TEvent>(TEvent payload) where TEvent : notnull
    {
        _newTransaction.Enqueue(payload);
    }

    public void Enqueue(string eventName, object payload)
    {
        _newTransaction.Enqueue(eventName, payload);
    }

    public void Dispose()
    {
        _newTransaction.Dispose();
    }
}
