# Zonit.Messaging — Distributed Transport (design plan)

> Status: **plan only.** Nothing here is implemented yet. This document is the agreed
> target for making `Publish` in one service deliver to handlers in other services,
> without changing the in‑process default for apps that don't opt in.

## 1. Goal

Today an event published in a process is delivered only to handlers **in that same
process** (an in‑memory `System.Threading.Channels.Channel`). The goal is:

> Publish an event in micro‑service **A**; have it handled by `IEventHandler<T>`
> implementations living in micro‑services **B**, **C**, … — over a broker — while
> keeping the existing in‑process behaviour as the zero‑config default and staying
> **Native‑AOT‑safe** end to end.

Primary target: **Events (pub/sub, fan‑out)**. Tasks (distributed work queue) reuse the
same envelope/serializer/transport later; Commands (request/response) are out of scope for
v1 (that is RPC, a different reliability model).

## 2. Where it couples today

The publish path is a straight, synchronous, in‑proc chain with **no seam**:

```
IEventProvider.Publish<T>(payload)            // EventProvider.cs — void, fire-and-forget
   └─ IEventManager.Publish<T>(payload)        // EventManager.cs
        └─ PublishDirect(eventName, payload)    // EventManager.cs:65 — the single coupling point
             └─ EventSubscription<T>.Enqueue    // writes to Channel.CreateUnbounded<T>()
                  └─ worker loop → handler
```

Facts that shape the design:

- `Publish` returns **`void`** — no ack, no async, no failure signal (`IEventProvider.cs`).
- Event identity is `typeof(T).FullName` computed inline in 3–4 places
  (`EventManager.cs:151`, `EventTransaction.cs:35`, `TaskManager.cs:182,257`).
- Handler discovery is **per‑assembly, in‑proc**: source generators emit
  `EventHandlerRegistration<TEvent>` markers; `EventHandlerRegistrationHostedService`
  subscribes them to the local `EventManager` at startup. `EventHandlerRegistration<TEvent>.EventType`
  already enumerates *every event type that has a local handler* — exactly the data a
  broker needs to declare queue/topic bindings.
- Channels are **unbounded** (`EventManager.cs`, `TaskManager.cs`) — a remote producer
  could OOM a consumer. **This must be fixed before any network feed (see §9).**
- `EventTransaction` is an in‑proc sequential batcher (at‑most‑once; events are lost if the
  host dies mid‑dispatch). It is the natural hook for a **transactional outbox**.

## 3. Design principles

1. **Opt‑in, additive.** Adding a transport package changes behaviour; installing nothing
   keeps today's in‑proc channel. The default `IEventTransport` is `InProcEventTransport`.
2. **AOT‑safe end to end.** Serialization uses source‑generated `System.Text.Json`
   `JsonTypeInfo` (the SDK's existing AOT story). No reflection‑based serializers, no
   `Type.GetType(string)` on the wire path.
3. **The abstraction lives in `*.Abstractions`.** Interfaces and the envelope are
   interface/POCO‑only so broker packages depend on abstractions, not the engine.
4. **Wire contract is explicit and versioned.** A renamed CLR type must not silently break
   the wire — type identity is an explicit string, not `typeof(T).FullName`.
5. **Local fast path stays local.** A handler in the same process keeps going through the
   in‑memory channel; the transport is the *cross‑process* hop, not a replacement for it.

## 4. The seam

New interfaces (names provisional), all in `Zonit.Messaging.Events.Abstractions`:

```csharp
/// Outbound: hands an envelope to whatever carries it cross-process.
public interface IEventTransport
{
    ValueTask PublishAsync(EventEnvelope envelope, CancellationToken ct = default);
}

/// Inbound: a broker package pumps received envelopes through this to the local engine,
/// which deserializes and routes into the existing in-proc subscription channels.
public interface IInboundEventSink
{
    ValueTask DeliverAsync(EventEnvelope envelope, CancellationToken ct = default);
}

/// AOT-safe (de)serialization of the payload body. Default: source-gen JsonTypeInfo.
public interface IMessageSerializer
{
    string ContentType { get; }                                   // e.g. "application/json"
    ReadOnlyMemory<byte> Serialize(object payload, Type type);
    object Deserialize(ReadOnlyMemory<byte> body, Type type);
}
```

The envelope — everything a receiver needs without touching CLR types until routing:

```csharp
public sealed record EventEnvelope
{
    public required Guid           MessageId     { get; init; } // idempotency / dedup key
    public required string         TypeId        { get; init; } // stable wire id (see §5)
    public required int            SchemaVersion { get; init; } // payload schema version
    public required DateTimeOffset OccurredAt    { get; init; }
    public required ReadOnlyMemory<byte> Body    { get; init; }
    public string                  ContentType   { get; init; } = "application/json";

    public Guid?   CorrelationId { get; init; } // ties a whole flow together
    public Guid?   CausationId   { get; init; } // the message that caused this one
    public Guid?   TenantId      { get; init; } // enforced server-side, NEVER trusted from wire
    public IReadOnlyDictionary<string, string> Headers { get; init; }
        = new Dictionary<string, string>();
}
```

### Wiring

- `EventManager.PublishDirect` is refactored so the cross‑process hop goes through
  `IEventTransport`; the **in‑proc fan‑out stays exactly as today**. Concretely: the default
  `InProcEventTransport` *is* the current channel write, registered with
  `TryAddSingleton<IEventTransport, InProcEventTransport>()` so installing nothing preserves
  behaviour. A broker package registers its own `IEventTransport` (last‑wins or composite).
- Add an **awaitable** publish to the public API for durable‑accept semantics:
  ```csharp
  Task PublishAsync<TEvent>(TEvent payload, CancellationToken ct = default);
  ```
  Keep `void Publish` as the in‑proc convenience. For distribution, `PublishAsync` completes
  only after the transport has **durably accepted** the message (broker ack, or an outbox row
  committed inside the caller's DB transaction — see §7).
- Inbound: a broker‑specific `IHostedService` receives raw messages, builds an
  `EventEnvelope`, and calls `IInboundEventSink.DeliverAsync`, which deserializes the body via
  `IMessageSerializer` and routes it into the **existing** local subscription channels — so
  remote and local delivery converge on one code path.

## 5. Type identity & versioning

`typeof(T).FullName` is fine as a dictionary key in one process; it is a **fragile wire
contract** (renames/moves break consumers). Introduce:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class EventTypeAttribute(string id, int version = 1) : Attribute { ... }

// usage
[EventType("orders.order-placed", version: 2)]
public record OrderPlacedEvent(Guid OrderId, ...);
```

- Centralize resolution in one `EventTypeName.Of<T>()` helper (replacing the inline
  `typeof(T).FullName ?? .Name` scattered today) that prefers `[EventType]`, falling back to
  `FullName` when absent.
- The **source generator** emits a static `TypeId → resolve` map so inbound routing is
  reflection‑free (AOT): the generator already sees every `[EventType]`‑decorated handler/event,
  so it can emit `switch (envelope.TypeId) { case "orders.order-placed": ... }` for both
  deserialization (`JsonTypeInfo`) and dispatch.
- `Schedule.FromBytes` returning `Empty` on an unknown version (`Schedule.cs`) is the right
  model: **reject/dead‑letter unknown `TypeId`/`SchemaVersion`, never throw into the pump.**

## 6. Delivery semantics

| Concern | v1 decision |
|---|---|
| Delivery guarantee | **At‑least‑once.** `PublishAsync` completes on durable accept; consumer acks after the handler succeeds. |
| Idempotency / dedup | Receiver keeps a **dedup window** keyed by `MessageId` (e.g. last N ids per consumer, or a `processed_messages` table). Re‑delivered messages are dropped. Handlers should still aim to be idempotent. |
| Ordering | **Not globally ordered** (today `WorkerCount=10` already reorders in‑proc). Offer **per‑key ordering** via a partition key header (e.g. `OrderId`) → single‑worker per key, or broker‑native partitioning. Document the guarantee explicitly. |
| Poison messages | After max delivery attempts → **dead‑letter** (see §10), never an infinite redelivery loop. |
| Schema mismatch | Unknown `TypeId`/`SchemaVersion` → dead‑letter + log, do not crash the pump. |

## 7. Transactional outbox (reliable publish)

The classic dual‑write problem: writing to the DB **and** publishing to a broker is not atomic.
`EventTransaction` is the seam to solve it without exposing the broker to the caller:

1. `CreateTransaction()` → `Publish(...)` enqueues events (as today).
2. `CommitAsync()` (inside the caller's DB transaction) writes the envelopes to a durable
   **outbox table** instead of dispatching them in‑proc.
3. A background **relay** (`IHostedService`) reads unsent outbox rows, calls
   `IEventTransport.PublishAsync`, and marks them sent (at‑least‑once; the relay is the only
   thing that talks to the broker).

This makes "the row was written ⟺ the event will be published" hold across a crash. The
outbox store is pluggable (`IOutboxStore`); EF Core / Dapper adapters ship as separate packages.
In‑proc apps that don't register an outbox keep the current direct‑dispatch behaviour.

## 8. Consumer discovery / topology

- `EventHandlerRegistration<TEvent>.EventType` already lists every event a service handles.
  At startup a transport‑aware hosted service enumerates these and **declares broker bindings**
  (queues/subscriptions) for them — so a service only subscribes to events it actually handles.
- No central registry is required for v1: each service declares its own bindings from its own
  generated registrations. (A future `messaging-topology` doc/endpoint can aggregate them for
  observability.)

## 9. Backpressure (prerequisite, do first)

The unbounded channels are safe only because the producer is in‑proc and self‑limiting. Once a
**remote** producer feeds them, an unbounded channel is a memory‑exhaustion DoS. Before any
network intake:

- Make the channel mode configurable: `BoundedChannelOptions { Capacity, FullMode }`
  (default to a sane bound, e.g. a few thousand, `FullMode = Wait`).
- Stop discarding `Writer.TryWrite`'s return value; on a full bounded channel, apply
  backpressure (await `WriteAsync`) or shed + metric.
- Surface a **queue‑depth metric** so operators can see saturation.

This is tracked as a standalone robustness fix (it is also worth doing for the in‑proc case)
and is a **hard gate** for the inbound pump.

## 10. Dead‑letter & failure handling

- Inbound pump: max delivery attempts → move the raw envelope to a **dead‑letter** queue/table
  with the failure reason and stack; expose it for inspection/replay.
- Reuse the per‑handler `ContinueOnError`/timeout semantics that already exist for the in‑proc
  worker loop; the difference for transport is that a terminal failure **nacks/dead‑letters**
  rather than just logging and draining.

## 11. Proposed package layout

```
Zonit.Messaging.Events.Abstractions      // + IEventTransport, IInboundEventSink,
                                          //   IMessageSerializer, EventEnvelope, [EventType]
Zonit.Messaging.Events                    // InProcEventTransport (default) + outbox relay host
Zonit.Messaging.Transport.Json            // default source-gen JsonTypeInfo IMessageSerializer
Zonit.Messaging.Transport.RabbitMq        // IEventTransport + inbound pump (one per broker)
Zonit.Messaging.Transport.AzureServiceBus //   "
Zonit.Messaging.Transport.Outbox.EfCore   // IOutboxStore adapter
```

Broker/serializer packages depend only on `*.Abstractions`. Each must itself be AOT‑clean.

## 12. Phased rollout

1. **Phase 0 — prerequisites (no transport yet):** centralize `EventTypeName.Of<T>()`; add
   `[EventType]`; make channels bounded (§9); add `Task PublishAsync` to the API (in‑proc impl
   first). Ships value on its own, breaks nothing.
2. **Phase 1 — the seam:** introduce `IEventTransport` (+ `InProcEventTransport` default),
   `EventEnvelope`, `IMessageSerializer` (+ JSON source‑gen default), `IInboundEventSink`.
   Still single‑process, but the path is now transport‑shaped and fully tested.
3. **Phase 2 — first broker:** one `Zonit.Messaging.Transport.*` package (RabbitMQ or ASB):
   outbound publish + inbound pump + binding declaration from registrations; at‑least‑once +
   `MessageId` dedup + dead‑letter.
4. **Phase 3 — reliability:** transactional outbox (§7) with an EF Core adapter; per‑key
   ordering; metrics (queue depth, publish/consume rates, DLQ size).
5. **Phase 4 — extend:** distributed Tasks over the same envelope/transport; optional
   request/response (Commands over RPC) if needed.

## 13. Open questions

- Default serializer only JSON, or also a compact binary (MessagePack) option? (JSON first.)
- Dedup store: in‑memory window (simple, per‑instance) vs. shared table (correct across
  instances)? Probably pluggable `IDedupStore`, in‑memory default.
- Do we want a single `IEventTransport` last‑wins, or a composite that fans out to multiple
  transports (e.g. in‑proc **and** broker)? Composite is more flexible; start last‑wins.
- Topic/exchange naming convention derived from `TypeId` — confirm the scheme before Phase 2.

---

### Audit cross‑reference

This plan addresses these audit findings: `no-transport-seam`, `publish-is-void-fire-and-forget`,
`no-envelope-or-serializer`, `no-cross-service-consumer-discovery`,
`transaction-not-distributed-outbox`, `unbounded-channels-no-backpressure` (prerequisite),
`extensionid-advisory-cross-tenant-leak` (TenantId must be server‑enforced),
`transport-readiness-no-auth-idempotency-versioning`.
