<div align="center">

# Zonit.Messaging

**Lightweight in-process messaging for .NET: Commands (CQRS), Events (pub/sub), Tasks (background jobs) and Schedules (recurring jobs).**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE)

</div>

---

Four small, independent libraries that share one idea: you write a handler class, the library
finds it at compile time and wires it into DI. There is no runtime reflection and no assembly
scanning, so everything works under trimming and Native AOT. Pick only the patterns you need.

```csharp
// 1. Register (once, anywhere in your DI setup)
services.AddCommandHandlers();
services.AddEventHandlers();
services.AddTaskHandlers();
services.AddScheduleHandlers();

// 2. Write a handler. A source generator discovers and registers it.
public sealed class SendWelcome : IEventHandler<UserCreated>
{
    public Task HandleAsync(UserCreated data, CancellationToken ct) => /* ... */;
}

// 3. Send.
eventProvider.Publish(new UserCreated(userId, email));
```

> Full guides for every pattern live in [`Instruction/`](./Instruction). Installing a package also
> teaches your AI coding assistant (Claude Code, Copilot, Cursor) how to use the library.

## Packages

Install only what you use. Each pattern has an `*.Abstractions` package so your domain layer can
depend on the contracts without the implementation.

| Package | Version | Downloads | Use it for |
|:---|:---|:---|:---|
| **Zonit.Messaging.Commands** | [![v](https://img.shields.io/nuget/v/Zonit.Messaging.Commands.svg?label=)](https://www.nuget.org/packages/Zonit.Messaging.Commands) | ![dt](https://img.shields.io/nuget/dt/Zonit.Messaging.Commands.svg?label=) | CQRS request, one handler, typed response |
| **Zonit.Messaging.Events** | [![v](https://img.shields.io/nuget/v/Zonit.Messaging.Events.svg?label=)](https://www.nuget.org/packages/Zonit.Messaging.Events) | ![dt](https://img.shields.io/nuget/dt/Zonit.Messaging.Events.svg?label=) | Pub/Sub fan-out to many handlers |
| **Zonit.Messaging.Tasks** | [![v](https://img.shields.io/nuget/v/Zonit.Messaging.Tasks.svg?label=)](https://www.nuget.org/packages/Zonit.Messaging.Tasks) | ![dt](https://img.shields.io/nuget/dt/Zonit.Messaging.Tasks.svg?label=) | Background jobs with progress and retries |
| **Zonit.Messaging.Schedules** | [![v](https://img.shields.io/nuget/v/Zonit.Messaging.Schedules.svg?label=)](https://www.nuget.org/packages/Zonit.Messaging.Schedules) | ![dt](https://img.shields.io/nuget/dt/Zonit.Messaging.Schedules.svg?label=) | Recurring jobs on a typed schedule |

```powershell
dotnet add package Zonit.Messaging.Commands
dotnet add package Zonit.Messaging.Events
dotnet add package Zonit.Messaging.Tasks
dotnet add package Zonit.Messaging.Schedules
```

## Register

Call the matching method for each pattern you use, in `Program.cs` or any plugin's DI module. The
calls are safe to repeat and work with or without handlers present. A source generator finds your
handler classes at compile time and emits concrete, reflection-free registration; these methods
wire it into DI. In a modular app, call them inside each plugin so the generator registers the
handlers in that compilation.

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

## Commands (CQRS)

A request handled by exactly one handler that returns a typed response. See
[Instruction/commands.md](./Instruction/commands.md).

```csharp
public record CreateUser(string Name, string Email) : IRequest<Guid>;

public sealed class CreateUserHandler : IRequestHandler<CreateUser, Guid>
{
    public async Task<Guid> HandleAsync(CreateUser request, CancellationToken ct = default)
        => await _users.AddAsync(request.Name, request.Email, ct);
}

// send (response type inferred from the request)
Guid id = await commandProvider.SendAsync(new CreateUser("Ada", "ada@example.com"));
```

## Events (Pub/Sub)

One event, many handlers (fan-out). Tune workers or timeout by declaring the matching property on
the handler; `IEventHandler<T>` exposes them as default interface members, so no base class is
needed. See [Instruction/events.md](./Instruction/events.md).

```csharp
public record UserCreated(Guid UserId, string Email);

public sealed class SendWelcome : IEventHandler<UserCreated>
{
    public int WorkerCount => 4;                       // optional override (default 10)
    public Task HandleAsync(UserCreated data, CancellationToken ct) => /* ... */;
}

eventProvider.Publish(new UserCreated(id, "ada@example.com"));
```

Group events into a transaction to dispatch them in order; in async code use `await using`:

```csharp
await using var tx = eventProvider.CreateTransaction();
eventProvider.Publish(new OrderCreated(orderId));
eventProvider.Publish(new InventoryReserved(orderId));
await tx.WaitForCompletionAsync();
```

## Tasks (background jobs)

Queue long-running work with progress reporting, retries and live monitoring. See
[Instruction/tasks.md](./Instruction/tasks.md).

```csharp
public sealed class ImportHandler : TaskHandler<ImportData>
{
    public override TaskProgressStep[]? ProgressSteps =>
        [new(TimeSpan.FromSeconds(5), "Connecting..."), new(TimeSpan.FromSeconds(20), "Saving...")];

    protected override async Task HandleAsync(ImportData d, ITaskProgressContext p, CancellationToken ct)
    {
        await p.NextAsync();   // connect
        await p.NextAsync();   // save
    }
}

taskProvider.Publish(new ImportData("data.csv", 1000));
taskManager.OnChange<ImportData>(s => Console.WriteLine($"{s.Progress}% {s.Message}"));
```

## Schedules (recurring jobs)

Run work on a typed, cron-like schedule. Each run gets a fresh DI scope and never overlaps itself.
See [Instruction/schedules.md](./Instruction/schedules.md).

```csharp
public sealed class DailyCleanup : IScheduleHandler
{
    public Task HandleAsync(CancellationToken ct) => /* ... */;
}

services.AddSchedule<DailyCleanup>(o =>
{
    o.Schedules = [Schedule.Now(), Schedule.EveryDay(3, 0)];
});
```

## Distributed transport (planned)

Today delivery is in-process only. Cross-service events over a broker (publish in service A, handle
in services B and C) are designed but not yet implemented. See
[docs/transport-plan.md](./docs/transport-plan.md). Until then, do not assume cross-process
delivery, durability or ordering.

## Requirements and license

Requires **.NET 10**. Released under the **MIT License**.

Issues and pull requests are welcome at
[github.com/Zonit/Zonit.Services.EventMessage](https://github.com/Zonit/Zonit.Services.EventMessage).
