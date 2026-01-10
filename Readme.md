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

### Simple Registration (Recommended)

Just call the registration methods - they work with or without handlers:

```csharp
using Zonit.Messaging.Commands;
using Zonit.Messaging.Events;
using Zonit.Messaging.Tasks;

// In your DI configuration (Program.cs or plugin registration)
services.AddCommandHandlers();  // Registers Commands (CQRS)
services.AddEventHandlers();    // Registers Events (Pub/Sub)  
services.AddTaskHandlers();     // Registers Tasks (Background Jobs)
```

**That's it!** These methods:
- ? **Always work** - even without any handlers
- ? **Auto-discover handlers** - Source Generator automatically registers handlers at compile-time
- ? **Safe to call multiple times** - uses `TryAdd` to prevent duplicates
- ? **AOT/Trimming safe** - no runtime reflection

### Plugin Architecture

For modular applications, call the methods in each plugin:

```csharp
// In Kemavo.Plugins.Catalogs.Application
namespace Kemavo.Plugins;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogsPlugin(this IServiceCollection services)
    {
        // These methods exist in Zonit.Messaging.* libraries
        // Source Generator automatically adds handlers from this assembly
        services.AddCommandHandlers();
        services.AddEventHandlers();
        services.AddTaskHandlers();
        
        return services;
    }
}

// In Program.cs
services.AddCatalogsPlugin();
services.AddWalletsPlugin();
services.AddAIPlugin();
```

### Manual Handler Registration

For fine-grained control, you can register handlers explicitly:

```csharp
// Commands
services.AddCommandHandlers();
services.AddCommand<CreateUserHandler>();  // Additional manual handler

// Events
services.AddEventHandlers();
services.AddEventHandler<UserCreatedHandler, UserCreatedEvent>();

// Tasks
services.AddTaskHandlers();
services.AddTaskHandler<SendEmailHandler, SendEmailTask>();
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

### 1. Define Event Handler (Recommended)

Create a class implementing `IEventHandler<T>` for automatic registration:

```csharp
public record UserCreatedEvent(string Name, string Email);

public class UserCreatedEventHandler : IEventHandler<UserCreatedEvent>
{
    private readonly ILogger<UserCreatedEventHandler> _logger;
    
    public UserCreatedEventHandler(ILogger<UserCreatedEventHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync(UserCreatedEvent data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User created: {Name}", data.Name);
        await SendWelcomeEmailAsync(data.Email, cancellationToken);
    }
}
```

### 2. Subscribe Manually (Alternative)

```csharp
var eventManager = serviceProvider.GetRequiredService<IEventManager>();

eventManager.Subscribe<UserCreatedEvent>(async (data, cancellationToken) =>
{
    Console.WriteLine($"User created: {data.Name}");
    await Task.CompletedTask;
});
```

### 3. Publish Events

```csharp
var eventProvider = serviceProvider.GetRequiredService<IEventProvider>();
eventProvider.Publish(new UserCreatedEvent("John", "john@example.com"));
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

Long-running operations with retry support and **real-time progress tracking**.

### 1. Simple Task Handler

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

### 2. Task Handler with Progress Tracking

Create handlers with automatic progress reporting based on estimated step durations:

```csharp
public class ImportDataHandler : TaskHandler<ImportDataTask>
{
    public override int WorkerCount => 2;
    public override TimeSpan Timeout => TimeSpan.FromMinutes(10);
    
    // Optional: Display title and description in UI
    public override string? Title => "Import Data";
    public override string? Description => "Importing data from external source";

    // Define steps with estimated durations for smooth progress calculation
    public override TaskProgressStep[]? ProgressSteps =>
    [
        new(TimeSpan.FromSeconds(5), "Connecting to source..."),
        new(TimeSpan.FromSeconds(10), "Downloading data..."),
        new(TimeSpan.FromSeconds(15), "Processing records..."),
        new(TimeSpan.FromSeconds(5), "Saving to database...")
    ];

    protected override async Task HandleAsync(
        ImportDataTask data,
        ITaskProgressContext progress,
        CancellationToken cancellationToken)
    {
        // Step 1: Connect (0% -> 14%)
        await progress.NextAsync();
        await ConnectAsync(cancellationToken);

        // Step 2: Download (14% -> 43%)
        await progress.NextAsync();
        await DownloadAsync(data.Url, cancellationToken);

        // Step 3: Process (43% -> 86%)
        await progress.NextAsync();
        for (int i = 0; i < data.RecordCount; i++)
        {
            await ProcessRecordAsync(i, cancellationToken);
            // Update message without changing step
            await progress.SetMessageAsync($"Processing {i + 1}/{data.RecordCount}...");
        }

        // Step 4: Save (86% -> 100%)
        await progress.NextAsync();
        await SaveAsync(cancellationToken);
    }
}
```

With Source Generators, registration is automatic:

```csharp
// In Program.cs - handlers are auto-discovered and registered
services.AddTaskHandlers();
```

### 3. Publish Tasks

```csharp
var taskProvider = serviceProvider.GetRequiredService<ITaskProvider>();

// Simple publish
taskProvider.Publish(new SendEmailTask { To = "user@example.com", Subject = "Welcome!" });

// Publish with ExtensionId for filtering
var organizationId = Guid.NewGuid();
taskProvider.Publish(new ImportDataTask("data.csv", 1000), organizationId);
```

### 4. Monitoring Task Progress

Subscribe to real-time progress updates:

```csharp
var taskManager = serviceProvider.GetRequiredService<ITaskManager>();

// Monitor all tasks
taskManager.OnChange(state =>
{
    Console.WriteLine($"Task {state.TaskType}: {state.Progress}% - {state.Message}");
    Console.WriteLine($"Duration: {state.Duration}");
});

// Monitor specific task type with typed data access
taskManager.OnChange<ImportDataTask>(state =>
{
    Console.WriteLine($"Import from {state.Data.Source}: {state.Progress}%");
    Console.WriteLine($"Step {state.CurrentStep}/{state.TotalSteps}: {state.Message}");
    Console.WriteLine($"Running for: {state.Duration?.TotalSeconds:F1}s");
});

// Monitor tasks for specific ExtensionId (e.g., organization)
taskManager.OnChange(organizationId, state =>
{
    // Only tasks published with this ExtensionId
    UpdateProgressBar(state.Progress ?? 0);
});

// Monitor specific type for specific ExtensionId
taskManager.OnChange<ImportDataTask>(organizationId, state =>
{
    // Typed access + filtered by ExtensionId
    UpdateUI(state.Data, state.Progress);
});
```

### 5. TaskState Properties

| Property | Type | Description |
|----------|------|-------------|
| `TaskId` | `Guid` | Unique task identifier |
| `ExtensionId` | `Guid?` | Optional identifier for filtering (e.g., user/organization ID) |
| `TaskType` | `string` | Full type name of the task |
| `Title` | `string?` | Optional display title for the task (null = uses TaskType) |
| `Description` | `string?` | Optional description of what the task does |
| `Status` | `TaskStatus` | Current status (Pending, Processing, Completed, Failed, Cancelled) |
| `Progress` | `int?` | Progress 0-100 (null if not tracked) |
| `CurrentStep` | `int?` | Current step number (1-based) |
| `TotalSteps` | `int?` | Total number of steps |
| `Message` | `string?` | Current status message |
| `CreatedAt` | `DateTimeOffset` | When task was created |
| `StartedAt` | `DateTimeOffset?` | When processing started |
| `CompletedAt` | `DateTimeOffset?` | When processing finished |
| `Duration` | `TimeSpan?` | Time elapsed since start |

### 6. Viewing Active Tasks

```csharp
// Get all active tasks
var activeTasks = taskManager.GetActiveTasks();

// Get active tasks for specific ExtensionId
var orgTasks = taskManager.GetActiveTasks(organizationId);

foreach (var task in activeTasks)
{
    Console.WriteLine($"{task.TaskType}: {task.Status} ({task.Progress}%) - {task.Duration}");
}

// Get specific task state
var state = taskManager.GetTaskState(taskId);
```

### 7. Task Lifecycle

Tasks go through various states during their lifecycle:

- **Pending** - Task is queued but not yet started
- **Processing** - Task is currently being processed
- **Completed** - Task finished successfully
- **Failed** - Task failed during processing
- **Cancelled** - Task was cancelled before completion

### 8. Progress Tracking Features

- **Time-based smooth progress**: Progress is automatically calculated based on estimated step durations
- **Automatic updates**: Progress updates are sent automatically (max 100 times, when % changes)
- **Step tracking**: Know which step is currently executing and how many remain
- **Duration tracking**: Real-time duration available via `Duration` property
- **Typed access**: Use `OnChange<T>` to get typed access to task data
- **Efficient filtering**: Filter by `ExtensionId` at the system level for better performance

---

## Migration from Legacy API

If upgrading from `Zonit.Services.EventMessage`:

| Legacy (deprecated) | New |
|---------------------|-----|
| `using Zonit.Services.EventMessage;` | `using Zonit.Messaging.Events;` |
| `services.AddEventMessageService()` | `services.AddEventHandlers()` |
| `EventBase<T>` | `IEventHandler<T>` |
| `TaskBase<T>` | `TaskHandler<T>` |
| `PayloadModel<T>` | Direct `TEvent data` parameter |
| `payload.Data` / `payload.CancellationToken` | `(data, cancellationToken)` parameters |

### Event Handler Migration Example

**Before (Legacy):**
```csharp
public class UserHandler : EventBase<UserCreatedEvent>
{
    protected override async Task HandleAsync(UserCreatedEvent data, CancellationToken ct)
    {
        // Handle event
    }
}
```

**After (New API):**
```csharp
public class UserHandler : IEventHandler<UserCreatedEvent>
{
    public async Task HandleAsync(UserCreatedEvent data, CancellationToken cancellationToken)
    {
        // Handle event - same signature, cleaner interface!
    }
}
```

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
