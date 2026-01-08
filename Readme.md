# Zonit.Messaging

A lightweight, high-performance .NET library for building **event-driven** and **CQRS** architectures with full **AOT/Trimming support**.

---

## :package: NuGet Packages

### Current Packages (Zonit.Messaging)

| Package | Version | Downloads | Description |
|---------|---------|-----------|-------------|
| **Zonit.Messaging.Commands** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Commands.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Messaging.Commands.svg) | CQRS Commands & Queries |
| **Zonit.Messaging.Commands.Abstractions** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Commands.Abstractions.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Messaging.Commands.Abstractions.svg) | Command interfaces |
| **Zonit.Messaging.Events** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Events.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Messaging.Events.svg) | Pub/Sub Events |
| **Zonit.Messaging.Events.Abstractions** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Events.Abstractions.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Messaging.Events.Abstractions.svg) | Event interfaces |
| **Zonit.Messaging.Tasks** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Tasks.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Messaging.Tasks.svg) | Background Jobs |
| **Zonit.Messaging.Tasks.Abstractions** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Tasks.Abstractions.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Messaging.Tasks.Abstractions.svg) | Task interfaces |

### Legacy Packages (deprecated)

| Package | Version | Downloads | Status |
|---------|---------|-----------|--------|
| **Zonit.Services.EventMessage** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Services.EventMessage.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Services.EventMessage.svg) | :warning: Deprecated |
| **Zonit.Services.EventMessage.Abstractions** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Services.EventMessage.Abstractions.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Services.EventMessage.Abstractions.svg) | :warning: Deprecated |

```powershell
# Install current packages
dotnet add package Zonit.Messaging.Commands
dotnet add package Zonit.Messaging.Events
dotnet add package Zonit.Messaging.Tasks

# Or via NuGet Package Manager
Install-Package Zonit.Messaging.Commands
Install-Package Zonit.Messaging.Events
Install-Package Zonit.Messaging.Tasks
```

---

## Features

- **Commands (CQRS)** - Request/Response pattern with strongly-typed handlers
- **Events (Pub/Sub)** - Publish events to multiple subscribers (fan-out)
- **Tasks (Background Jobs)** - Queue long-running operations with retry support
- **Transaction Support** - Group events into transactions to be processed sequentially
- **AOT-Safe** - Full Native AOT and trimming support via Source Generators
- **Concurrent Processing** - Control the number of concurrently executed handlers
- **Timeout Handling** - Configure timeouts for processing

---

## Requirements

- .NET 8, .NET 9 or .NET 10

---

## Quick Start

Add services to your application:

```csharp
using Zonit.Messaging.Commands;
using Zonit.Messaging.Events;
using Zonit.Messaging.Tasks;

// Commands (CQRS)
services.AddCommandProvider();
services.AddCommand<CreateUserHandler>();

// Events (Pub/Sub)
services.AddEventProvider();

// Tasks (Background Jobs)
services.AddTaskProvider();
```

---

## Commands (CQRS)

Request/Response pattern - send a request, get a typed response.

### 1. Define a Command

```csharp
public record CreateUserCommand(string Name, string Email) : IRequest<Guid>;
```

### 2. Implement Handler

```csharp
public class CreateUserHandler : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreateUserCommand request, CancellationToken ct = default)
    {
        var userId = Guid.NewGuid();
        // Save to database...
        return userId;
    }
}
```

### 3. Send Command

```csharp
var commandProvider = serviceProvider.GetRequiredService<ICommandProvider>();
var userId = await commandProvider.SendAsync(new CreateUserCommand("John", "john@example.com"));
```

---

## Events (Pub/Sub)

Publish events to multiple subscribers asynchronously.

### 1. Subscribe to Events

```csharp
var eventManager = serviceProvider.GetRequiredService<IEventManager>();

eventManager.Subscribe<UserCreatedEvent>(async payload =>
{
    Console.WriteLine($"User created: {payload.Data.Name}");
});
```

### 2. Publish Events

```csharp
var eventProvider = serviceProvider.GetRequiredService<IEventProvider>();
eventProvider.Publish(new UserCreatedEvent { Name = "John" });
```

### 3. Using Transactions

Group events to be processed sequentially:

```csharp
using (var transaction = eventProvider.CreateTransaction())
{
    eventProvider.Publish(new Event1());
    eventProvider.Publish(new Event2());
    // Events are queued until the transaction is completed
}
// Events are processed after the transaction is disposed
```

### 4. Awaiting Transaction Completion

Wait for all events in a transaction to be processed:

```csharp
using (var transaction = eventProvider.CreateTransaction())
{
    eventProvider.Publish(new OrderCreatedEvent());
    eventProvider.Publish(new InventoryUpdatedEvent());
    
    // Wait for all events to be processed before continuing
    await transaction.WaitForCompletionAsync();
}
```

---

## Tasks (Background Jobs)

Long-running operations with retry support.

### 1. Subscribe to Tasks

```csharp
var taskManager = serviceProvider.GetRequiredService<ITaskManager>();

taskManager.Subscribe<SendEmailTask>(async payload =>
{
    await SendEmailAsync(payload.Data.To, payload.Data.Subject);
}, new TaskSubscriptionOptions
{
    WorkerCount = 5,
    MaxRetries = 3,
    RetryDelay = TimeSpan.FromSeconds(10)
});
```

### 2. Publish Tasks

```csharp
var taskProvider = serviceProvider.GetRequiredService<ITaskProvider>();
taskProvider.Publish(new SendEmailTask { To = "user@example.com", Subject = "Welcome!" });
```

### 3. Monitoring Task Status

Subscribe to task status change events:

```csharp
var taskManager = serviceProvider.GetRequiredService<ITaskManager>();

taskManager.EventOnChange(async (taskEvent) =>
{
    Console.WriteLine($"Task {taskEvent.Id} status changed to {taskEvent.Status}");
});
```

### 4. Viewing Active Tasks

Retrieve and display the list of active tasks:

```csharp
var activeTasks = taskManager.GetActiveTasks();
foreach (var task in activeTasks)
{
    Console.WriteLine($"Active Task: {task.Id} - {task.Status}");
}
```

### 5. Task Lifecycle

Tasks go through various states during their lifecycle:

- **Pending** - Task is queued but not yet started
- **Processing** - Task is currently being processed
- **Completed** - Task finished successfully
- **Failed** - Task failed during processing
- **Cancelled** - Task was cancelled before completion

---

## Source Generators (AOT-Safe)

Auto-register handlers at compile time for Native AOT support:

```csharp
services.AddCommandHandlers();
services.AddEventHandlers();
services.AddTaskHandlers();
```

---

## Migration from Legacy API

If upgrading from `Zonit.Services.EventMessage`:

| Legacy (deprecated) | New |
|---------------------|-----|
| `using Zonit.Services.EventMessage;` | `using Zonit.Messaging.Events;` |
| `services.AddEventMessageService()` | `services.AddEventProvider()` |
| `EventBase<T>` | `IEventHandler<T>` |
| `TaskBase<T>` | `ITaskHandler<T>` |
| `PayloadModel<T>` | `EventPayload<T>` / `TaskPayload<T>` |

Legacy code continues to work but shows deprecation warnings.

---

## Example Use Cases

- Decoupling logic between independent application components
- Implementing event-driven workflows
- Handling system notifications and real-time updates
- Creating robust background job queues
- CQRS architecture implementation

---

## Contributing & Support

Found a bug or have a feature request? Open an [issue](https://github.com/Zonit/Zonit.Services.EventMessage/issues/new) on GitHub!

---

## License

[MIT](LICENSE)
