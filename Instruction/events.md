# Events (Pub/Sub)

Publish an event; every registered handler for that type receives it (fan-out). Delivery is
asynchronous and in-process: `Publish` returns immediately after queuing, and handlers run on
background workers off a channel.

## Define a handler

Implement `IEventHandler<TEvent>`. The event type is any class/record. Multiple handlers may
handle the same event.

```csharp
public record UserCreatedEvent(Guid UserId, string Email);

public sealed class SendWelcomeEmail : IEventHandler<UserCreatedEvent>
{
    private readonly IEmailSender _email;
    public SendWelcomeEmail(IEmailSender email) => _email = email;

    public Task HandleAsync(UserCreatedEvent data, CancellationToken cancellationToken)
        => _email.SendWelcomeAsync(data.Email, cancellationToken);
}
```

Register with `services.AddEventHandlers();` — the source generator discovers the class.

## Per-handler options

`IEventHandler<TEvent>` exposes three settings as **default interface members**. To override a
default, declare the matching property on your handler — that's it, no base class:

```csharp
public sealed class ImportOrders : IEventHandler<OrderPlacedEvent>
{
    public int WorkerCount => 2;                       // parallel workers (default 10)
    public TimeSpan Timeout => TimeSpan.FromMinutes(10); // per-invocation limit (default 5 min)
    public bool ContinueOnError => true;                // keep draining after a failure (default true)

    public Task HandleAsync(OrderPlacedEvent data, CancellationToken ct) => /* ... */;
}
```

| Property | Default | Meaning |
| :--- | :--- | :--- |
| `WorkerCount` | `10` | Parallel workers consuming this event type's channel |
| `Timeout` | `5 min` | Per-invocation limit; the token passed to `HandleAsync` is cancelled when it elapses. Use `Timeout.InfiniteTimeSpan` to disable (also skips per-message allocations) |
| `ContinueOnError` | `true` | Keep processing the channel after a handler throws (the error is logged) |

These are read once when the subscription is wired at startup. Explicit options passed to
`AddEvent<THandler, TEvent>(opts => ...)` take precedence over the handler's properties.

## Publish

Resolve `IEventProvider` and call `Publish`:

```csharp
public sealed class UserService(IEventProvider events)
{
    public void Register(string email)
    {
        // ... create the user ...
        events.Publish(new UserCreatedEvent(userId, email));
    }
}
```

`Publish` is `void` (fire-and-forget into the in-process channel). There is no cross-process
delivery yet — see [transport.md](./transport.md).

## Ad-hoc subscriptions

For handlers you don't want as classes, subscribe a delegate on `IEventManager`:

```csharp
eventManager.Subscribe<UserCreatedEvent>(async (data, ct) =>
{
    await audit.RecordAsync(data.UserId, ct);
});
```

## Transactions

Group events and dispatch them **sequentially** when the transaction completes. Events published
through `IEventProvider` while a transaction is active are captured automatically (ambient):

```csharp
await using var tx = events.CreateTransaction();

events.Publish(new OrderCreatedEvent(orderId));
events.Publish(new InventoryReservedEvent(orderId));

// Dispatch now and wait for all handlers to finish (in order):
await tx.WaitForCompletionAsync();
// (or just let `await using` dispatch on dispose)
```

In async code prefer `await using` — synchronous `using` has to bridge the async dispatch and
blocks the thread. A transaction is in-process and at-most-once: if the host dies mid-dispatch,
remaining events are lost (durable delivery is part of the planned [transport](./transport.md)).
