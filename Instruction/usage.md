# Getting started

Zonit.Messaging is four small, independent libraries for in-process messaging in .NET, all
**Native-AOT / trimming safe** because handler registration is generated at compile time by a
Roslyn source generator (no runtime reflection, no assembly scanning).

| Pattern | Package | Use it for |
| :--- | :--- | :--- |
| **Commands** (CQRS) | `Zonit.Messaging.Commands` | request → single handler → typed response |
| **Events** (Pub/Sub) | `Zonit.Messaging.Events` | publish → many handlers, fan-out, fire-and-forget |
| **Tasks** (background jobs) | `Zonit.Messaging.Tasks` | queue long-running work, progress, retries |
| **Schedules** (recurring jobs) | `Zonit.Messaging.Schedules` | run work on a cron-like schedule |

Install only what you use:

```powershell
dotnet add package Zonit.Messaging.Events
dotnet add package Zonit.Messaging.Commands
dotnet add package Zonit.Messaging.Tasks
dotnet add package Zonit.Messaging.Schedules
```

## Register

Call the registration method for each pattern you use, in `Program.cs` or any plugin's DI module.
They are safe to call repeatedly (they use `TryAdd`) and work even with **no** handlers present.

```csharp
using Zonit.Messaging.Commands;
using Zonit.Messaging.Events;
using Zonit.Messaging.Tasks;
using Zonit.Messaging.Schedules;

services.AddCommandHandlers();   // Commands (CQRS)
services.AddEventHandlers();     // Events (Pub/Sub)
services.AddTaskHandlers();      // Tasks (background jobs)
services.AddScheduleHandlers();  // Schedules (recurring jobs)  — alias of AddScheduleServices()
```

## Auto-discovery (how registration works)

You never hand-register a handler. Each package ships a source generator that, at **compile
time**, finds your handler classes and emits a `[ModuleInitializer]` that records them. The
`AddXxxHandlers()` call then wires those records into DI. Because everything is generated against
**concrete types**, it survives trimming and Native AOT with no warnings.

A handler is discovered when it:

- **Command**: implements `IRequestHandler<TRequest, TResponse>`
- **Event**: implements `IEventHandler<TEvent>`
- **Task**: derives from `TaskHandler<TTask>` (or implements `ITaskHandler<TTask>`)
- **Schedule**: implements `IScheduleHandler` or `IScheduleHandler<TData>`

Put the `AddXxxHandlers()` call in the assembly that contains the handlers (e.g. each plugin's DI
module); the generator registers handlers from that compilation.

```csharp
public static IServiceCollection AddCatalogPlugin(this IServiceCollection services)
{
    services.AddCommandHandlers();
    services.AddEventHandlers();
    services.AddTaskHandlers();
    return services;
}
```

## Manual registration (optional)

For explicit control you can register one handler at a time. These overloads are AOT-annotated
(`[DynamicallyAccessedMembers]`), so they are trimming-clean too:

```csharp
services.AddCommand<CreateUserHandler, CreateUserCommand, Guid>();
services.AddEvent<UserCreatedHandler, UserCreatedEvent>();
services.AddTask<SendEmailHandler, SendEmailTask>();
services.AddScheduleHandler<CleanupHandler, CleanupJobData>();
```

Next: [events.md](./events.md) · [commands.md](./commands.md) · [tasks.md](./tasks.md) ·
[schedules.md](./schedules.md) · [configuration.md](./configuration.md)
