# Configuration, lifetimes & AOT

## DI lifetimes

- **Managers / providers** (`IEventManager`, `IEventProvider`, `ITaskManager`, `ITaskProvider`,
  `ICommandProvider`, `IScheduleProvider`) are **singletons**.
- **Handlers** are **scoped**. Every dispatched event/task/schedule run opens a fresh DI scope and
  resolves the handler from it, so injecting a `DbContext` or other scoped service is safe — each
  invocation gets its own.

## Workers, timeouts, capacity

Events and tasks each run on a per-type channel drained by a pool of workers. Tune per handler
(properties on the handler) or per manual subscription (`EventSubscriptionOptions` /
`TaskSubscriptionOptions`):

| Option | Default | Notes |
| :--- | :--- | :--- |
| `WorkerCount` | `10` | Parallel workers for that event/task type |
| `Timeout` | `5 min` | Per-invocation limit, enforced via the `CancellationToken` passed to the handler. Set `Timeout.InfiniteTimeSpan` to disable it — this also takes a fast path that skips the per-message `CancellationTokenSource` allocations |
| `ContinueOnError` | `true` | Keep draining after a handler throws (error is logged) |
| `Capacity` | `null` (unbounded) | Max buffered messages for that type. Set a bound to cap memory when a producer can outrun the workers; once full, extra messages are dropped with a logged warning (publishing is synchronous, so it never blocks the publisher) |

`MaxRetries` / `RetryDelay` (tasks only) re-attempt a failed task before marking it `Failed`.

## AOT & trimming

Zonit.Messaging is built for Native AOT and trimming:

- **Auto-discovery** (`AddXxxHandlers()`) is generated against concrete types via a
  `[ModuleInitializer]` — no runtime reflection, no assembly scanning. This path is fully
  trimming/AOT-clean.
- **Manual** helpers (`AddEvent<H,E>`, `AddCommand<H,Rq,Rs>`, `AddTask<H,T>`,
  `AddSchedule<H>`, `AddScheduleHandler<H,D>`) annotate their handler type parameter with
  `[DynamicallyAccessedMembers(PublicConstructors)]`, so they are trimming-clean as well.
- The packages set `IsAotCompatible` / `IsTrimmable`, so the IL trim/AOT analyzers run on every
  build and the packages ship **zero IL warnings**.

There is nothing to configure for AOT — just publish with `PublishAot=true`.

## Where to call `AddXxxHandlers()`

Call it in the assembly that defines the handlers (each plugin's DI module). The generator
registers the handlers it finds **in that compilation**, so per-plugin calls compose cleanly:

```csharp
services.AddCatalogPlugin();   // each calls AddEventHandlers()/AddCommandHandlers()/...
services.AddBillingPlugin();
```
