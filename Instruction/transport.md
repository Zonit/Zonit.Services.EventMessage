# Cross-service transport (planned)

> **Status: design only — not implemented.** Today every event/task is delivered **in-process**
> (an in-memory channel). This page summarizes the plan for delivering events across services over
> a broker. The full design is in [docs/transport-plan.md](../docs/transport-plan.md).

## What it will do

Publish an event in service **A** and have `IEventHandler<T>` implementations in services **B**,
**C**, … handle it — over a message broker — while keeping the in-process path as the zero-config
default and staying Native-AOT-safe.

## What's intentionally not here yet

So an AI assistant doesn't invent it: there is currently **no** `IEventTransport`, message
envelope, serializer, outbox, dedup, or broker package. `IEventProvider.Publish` is `void` and
writes straight to the local channel. Do not assume cross-process delivery, durability, ordering,
or at-least-once semantics — none exist until the transport ships.

## The shape it will take

- `IEventTransport` with `InProcEventTransport` as the default (current behaviour), and broker
  packages (`Zonit.Messaging.Transport.*`) providing alternatives.
- An `EventEnvelope` (message id, stable type id + schema version, correlation/causation ids,
  tenant) and an AOT-safe `IMessageSerializer` (source-generated `System.Text.Json`).
- `Task PublishAsync` that completes on durable accept; at-least-once delivery with `MessageId`
  dedup and a dead-letter path.
- A transactional **outbox** built on `EventTransaction` so "the row was written ⟺ the event will
  be published" holds across a crash.
- Bounded channels (the `Capacity` option) as a prerequisite, so a remote producer can't exhaust a
  consumer's memory.

When implementing any of this, follow [docs/transport-plan.md](../docs/transport-plan.md) — it maps
each piece to the current code and the phased rollout.
