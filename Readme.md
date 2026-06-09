# Zonit.Messaging

Lightweight in-process messaging for .NET — **Commands** (CQRS), **Events** (pub/sub), **Tasks**
(background jobs) and **Schedules** (recurring jobs) — with first-class **Native AOT / trimming**
support. Handlers are discovered and registered at compile time by source generators, so there is
**no runtime reflection and no assembly scanning**.

## Packages

Install only the patterns you use. Each comes with an `*.Abstractions` package for projects that
should depend on the contracts without the implementation.

| Package | Version | Downloads | Use it for |
|---|---|---|---|
| **Zonit.Messaging.Commands** | [![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Commands.svg)](https://www.nuget.org/packages/Zonit.Messaging.Commands) | ![Downloads](https://img.shields.io/nuget/dt/Zonit.Messaging.Commands.svg) | CQRS request → one handler → typed response |
| **Zonit.Messaging.Events** | [![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Events.svg)](https://www.nuget.org/packages/Zonit.Messaging.Events) | ![Downloads](https://img.shields.io/nuget/dt/Zonit.Messaging.Events.svg) | Pub/Sub fan-out to many handlers |
| **Zonit.Messaging.Tasks** | [![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Tasks.svg)](https://www.nuget.org/packages/Zonit.Messaging.Tasks) | ![Downloads](https://img.shields.io/nuget/dt/Zonit.Messaging.Tasks.svg) | Background jobs with progress & retries |
| **Zonit.Messaging.Schedules** | [![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Schedules.svg)](https://www.nuget.org/packages/Zonit.Messaging.Schedules) | ![Downloads](https://img.shields.io/nuget/dt/Zonit.Messaging.Schedules.svg) | Recurring jobs on a typed schedule |

```powershell
dotnet add package Zonit.Messaging.Commands
dotnet add package Zonit.Messaging.Events
dotnet add package Zonit.Messaging.Tasks
dotnet add package Zonit.Messaging.Schedules
```

Requires **.NET 10**.

## Quick start

Register the patterns you use — once, anywhere in your DI setup. The calls are safe to repeat and
work with or without handlers present:

```csharp
using Zonit.Messaging.Commands;
using Zonit.Messaging.Events;
using Zonit.Messaging.Tasks;
using Zonit.Messaging.Schedules;

services.AddCommandHandlers();
services.AddEventHandlers();
services.AddTaskHandlers();
services.AddScheduleHandlers();
```

You never register handlers by hand. A source generator finds your handler classes at compile time
and emits concrete, reflection-free registration via a `[ModuleInitializer]`; `AddXxxHandlers()`
wires it into DI. In a modular app, put the calls in each plugin's DI module — the generator
registers the handlers in that compilation.

## The four patterns

### Commands (CQRS)

```csharp
public record CreateUser(string Name, string Email) : IRequest<Guid>;

public sealed class CreateUserHandler : IRequestHandler<CreateUser, Guid>
{
    public Task<Guid> HandleAsync(CreateUser request, CancellationToken ct = default) { /* ... */ }
}

// send
Guid id = await commandProvider.SendAsync(new CreateUser("Ada", "ada@example.com"));
```

### Events (Pub/Sub)

One event, many handlers (fan-out). Tune workers/timeout by declaring the matching property on the
handler — `IEventHandler<T>` exposes them as default interface members, no base class needed.

```csharp
public record UserCreated(Guid UserId, string Email);

public sealed class SendWelcome : IEventHandler<UserCreated>
{
    public int WorkerCount => 4;                          // optional override (default 10)
    public Task HandleAsync(UserCreated data, CancellationToken ct) { /* ... */ }
}

// publish
eventProvider.Publish(new UserCreated(id, "ada@example.com"));
```

Group events into a transaction to dispatch them in order; in async code use `await using`:

```csharp
await using var tx = eventProvider.CreateTransaction();
eventProvider.Publish(new OrderCreated(orderId));
eventProvider.Publish(new InventoryReserved(orderId));
await tx.WaitForCompletionAsync();
```

### Tasks (background jobs)

Queue long-running work with progress, retries and live monitoring.

```csharp
public sealed class ImportHandler : TaskHandler<ImportData>
{
    public override TaskProgressStep[]? ProgressSteps =>
        [ new(TimeSpan.FromSeconds(5), "Connecting..."), new(TimeSpan.FromSeconds(20), "Saving...") ];

    protected override async Task HandleAsync(ImportData d, ITaskProgressContext p, CancellationToken ct)
    {
        await p.NextAsync(); /* connect */
        await p.NextAsync(); /* save    */
    }
}

taskProvider.Publish(new ImportData("data.csv", 1000));
taskManager.OnChange<ImportData>(s => Console.WriteLine($"{s.Progress}% — {s.Message}"));
```

### Schedules (recurring jobs)

Run work on a typed, cron-like schedule. Each run gets a fresh DI scope and never overlaps itself.

```csharp
public sealed class DailyCleanup : IScheduleHandler
{
    public Task HandleAsync(CancellationToken ct) { /* ... */ }
}

services.AddSchedule<DailyCleanup>(o =>
{
    o.Schedules = [ Schedule.Now(), Schedule.EveryDay(3, 0) ];
});
```

See the [Instruction/](./Instruction) guides for the full API of each pattern.

## AOT & trimming

Built for Native AOT. The packages set `IsAotCompatible`/`IsTrimmable` and ship **zero IL
warnings** under the trim/AOT analyzers:

- **Auto-discovery** (`AddXxxHandlers()`) is generated against concrete types — no reflection, no
  assembly scanning.
- **Manual** helpers (`AddEvent<H,E>`, `AddCommand<H,Rq,Rs>`, `AddTask<H,T>`, `AddSchedule<H>`,
  `AddScheduleHandler<H,D>`) annotate their handler type parameter so they are trimming-clean too.

Just publish with `PublishAot=true` — there is nothing to configure.

## AI-assistant ready

These packages teach your AI coding assistant how to use them. At build time they project the
[Instruction/](./Instruction) guides into whatever assistant your repo uses — **Claude Code**
(`.claude/skills/` + a `CLAUDE.md` pointer), **GitHub Copilot** (`.github/instructions/`) and
**Cursor** (`.cursor/rules/`) — plus a neutral `.zonit/messaging/` copy. Detection is anchored at
the repo root and only writes for editors you actually use; nothing is produced on CI.

Opt out or steer it with MSBuild properties:

```xml
<ZonitMsgInstructions>false</ZonitMsgInstructions>   <!-- disable entirely -->
<ZonitMsgEditors>claude;cursor</ZonitMsgEditors>     <!-- auto (default) | all | none | a list -->
```

## Distributed transport (planned)

Today delivery is **in-process** only. Cross-service events over a broker (publish in service A,
handle in services B/C) are designed but **not yet implemented** — see
[docs/transport-plan.md](./docs/transport-plan.md). Until then, don't assume cross-process
delivery, durability or ordering.

## Contributing & support

Found a bug or have a feature request? Open an
[issue](https://github.com/Zonit/Zonit.Services.EventMessage/issues/new).

## License

[MIT](LICENSE)
