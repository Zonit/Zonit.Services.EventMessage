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
| **Zonit.Messaging.Schedules** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Schedules.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Messaging.Schedules.svg) | Recurring Jobs |
| **Zonit.Messaging.Schedules.Abstractions** | ![NuGet](https://img.shields.io/nuget/v/Zonit.Messaging.Schedules.Abstractions.svg) | ![NuGet](https://img.shields.io/nuget/dt/Zonit.Messaging.Schedules.Abstractions.svg) | Schedule interfaces |

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
dotnet add package Zonit.Messaging.Schedules

# Or via NuGet Package Manager
Install-Package Zonit.Messaging.Commands
Install-Package Zonit.Messaging.Events
Install-Package Zonit.Messaging.Tasks
Install-Package Zonit.Messaging.Schedules
```

---

## Features

- **Commands (CQRS)** - Request/Response pattern with strongly-typed handlers
- **Events (Pub/Sub)** - Publish events to multiple subscribers (fan-out)
- **Tasks (Background Jobs)** - Queue long-running operations with retry support
- **Schedules (Recurring Jobs)** - Execute jobs on schedule (cron-like, strongly-typed)
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
using Zonit.Messaging.Schedules;

// In your DI configuration (Program.cs or plugin registration)
services.AddCommandHandlers();  // Registers Commands (CQRS)
services.AddEventHandlers();    // Registers Events (Pub/Sub)  
services.AddTaskHandlers();     // Registers Tasks (Background Jobs)
services.AddScheduleServices(); // Registers Schedules (Recurring Jobs)
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
        services.AddScheduleServices();
        
        return services;
    }
}

// In Program.cs
services.AddCatalogsPlugin();
services.AddWalletsPlugin();
services.AddAIPlugin();
```

### Manual Handler Registration

For fine-grained control, you can register handlers explicitly (100% AOT-safe):

```csharp
// Commands - specify handler, request, and response types
services.AddCommand<CreateUserHandler, CreateUserCommand, Guid>();

// Events - specify handler and event types
services.AddEvent<UserCreatedHandler, UserCreatedEvent>();

// Tasks - specify handler and task types
services.AddTask<SendEmailHandler, SendEmailTask>();

// Schedules - specify handler and data types
services.AddScheduleHandler<CleanupHandler, CleanupJobData>();
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

### 2. Customize Per-Handler Defaults

If a handler needs different settings than the library defaults (longer timeout, different worker count), inherit from `EventHandler<T>` instead of implementing `IEventHandler<T>` directly. The base class exposes `virtual` properties read once at startup when the subscription is wired up.

```csharp
public class LongRunningOrderHandler : EventHandler<OrderPlacedEvent>
{
    public override TimeSpan Timeout => TimeSpan.FromMinutes(10);
    public override int WorkerCount => 2;
    public override bool ContinueOnError => true;

    private readonly IOrderProcessor _processor;

    public LongRunningOrderHandler(IOrderProcessor processor)
    {
        _processor = processor;
    }

    protected override Task HandleAsync(OrderPlacedEvent data, CancellationToken cancellationToken)
        => _processor.ProcessAsync(data, cancellationToken);
}
```

| Property | Default | Description |
|----------|---------|-------------|
| `WorkerCount` | `10` | Parallel workers consuming the channel |
| `Timeout` | `5 min` | Per-invocation execution limit (linked CTS) |
| `ContinueOnError` | `true` | Whether to keep draining the channel after an exception |

> **Source Generator** automatically detects classes inheriting from `EventHandler<T>` (they implement `IEventHandler<T>` transitively). No additional registration is required.

> **Override priority:** explicit options passed to `AddEvent<H, E>(opts => ...)` win over `EventHandler<T>` properties, which in turn win over library defaults.

### 3. Subscribe Manually (Alternative)

```csharp
var eventManager = serviceProvider.GetRequiredService<IEventManager>();

eventManager.Subscribe<UserCreatedEvent>(async (data, cancellationToken) =>
{
    Console.WriteLine($"User created: {data.Name}");
    await Task.CompletedTask;
});
```

### 4. Publish Events

```csharp
var eventProvider = serviceProvider.GetRequiredService<IEventProvider>();
eventProvider.Publish(new UserCreatedEvent("John", "john@example.com"));
```

### 5. Using Transactions

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

### 6. Awaiting Transaction Completion

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

// Monitor multiple task types at once (efficient filtering)
taskManager.OnChange<ImportDataTask, ExportDataTask>(state =>
{
    // Receives updates only for ImportDataTask or ExportDataTask
    UpdateProgress(state.TaskType, state.Progress);
});

// Monitor up to 4 types simultaneously
taskManager.OnChange<Task1, Task2, Task3, Task4>(state =>
{
    // Efficient server-side filtering
    NotifyUI(state);
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

// Get active tasks of specific type with typed data access
var importTasks = taskManager.GetActiveTasks<ImportDataTask>();
foreach (var task in importTasks)
{
    Console.WriteLine($"Import from {task.Data.Source}: {task.Progress}% - {task.Duration}");
}

// Get active tasks of multiple types
var dataTasks = taskManager.GetActiveTasks<ImportDataTask, ExportDataTask>();
var allProcessingTasks = taskManager.GetActiveTasks<Task1, Task2, Task3>();

// Get tasks filtered by type AND ExtensionId
var orgImports = taskManager.GetActiveTasks<ImportDataTask>(organizationId);

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
- **Multi-type monitoring**: Subscribe to multiple task types simultaneously (2-4 types) with single handler

### 9. ITaskManager API Reference

#### GetActiveTasks Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetActiveTasks()` | `IReadOnlyCollection<TaskState>` | All active tasks |
| `GetActiveTasks(extensionId)` | `IReadOnlyCollection<TaskState>` | Active tasks for specific ExtensionId |
| `GetActiveTasks<TTask>()` | `IReadOnlyCollection<TaskState<TTask>>` | Active tasks of specific type with typed data |
| `GetActiveTasks<TTask>(extensionId)` | `IReadOnlyCollection<TaskState<TTask>>` | Active tasks of type for ExtensionId |
| `GetActiveTasks<T1, T2>()` | `IReadOnlyCollection<TaskState>` | Active tasks of 2 types |
| `GetActiveTasks<T1, T2, T3>()` | `IReadOnlyCollection<TaskState>` | Active tasks of 3 types |
| `GetActiveTasks<T1, T2, T3, T4>()` | `IReadOnlyCollection<TaskState>` | Active tasks of 4 types |
| `GetTaskState(taskId)` | `TaskState?` | Specific task by ID |

#### OnChange Methods

| Method | Parameters | Description |
|--------|-----------|-------------|
| `OnChange(handler)` | `Action<TaskState>` | Monitor all task changes |
| `OnChange(extensionId, handler)` | `Guid, Action<TaskState>` | Monitor tasks for ExtensionId |
| `OnChange<TTask>(handler)` | `Action<TaskState<TTask>>` | Monitor specific task type with typed data |
| `OnChange<TTask>(extensionId, handler)` | `Guid, Action<TaskState<TTask>>` | Monitor type for ExtensionId |
| `OnChange<T1, T2>(handler)` | `Action<TaskState>` | Monitor 2 task types |
| `OnChange<T1, T2, T3>(handler)` | `Action<TaskState>` | Monitor 3 task types |
| `OnChange<T1, T2, T3, T4>(handler)` | `Action<TaskState>` | Monitor 4 task types |

All `OnChange` methods return `IDisposable` for unsubscribing.

**Example: Monitoring Multiple Types**

```csharp
// Efficient server-side filtering - only 2 types are monitored
var subscription = taskManager.OnChange<ImportTask, ExportTask>(state =>
{
    // This handler only receives ImportTask and ExportTask updates
    // Much more efficient than filtering in the handler
    Console.WriteLine($"{state.TaskType}: {state.Progress}%");
});

// Unsubscribe when done
subscription.Dispose();
```

---

## Schedules (Recurring Jobs)

Execute jobs on a recurring schedule using strongly-typed `Schedule` ValueObject (cron-like but with compile-time safety).

### 1. Define Handler

```csharp
public record CleanupJobData(string Directory, int RetentionDays);

public class CleanupHandler : IScheduleHandler<CleanupJobData>
{
    private readonly ILogger<CleanupHandler> _logger;

    public CleanupHandler(ILogger<CleanupHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(CleanupJobData data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleanup: {Dir}, retention: {Days} days", 
            data.Directory, data.RetentionDays);
        
        // Perform cleanup...
        await Task.CompletedTask;
    }
}
```

### 2. Register Handlers

```csharp
using Zonit.Messaging.Schedules;

// Option A: Automatic registration via Source Generator (Recommended)
services.AddScheduleServices();  // Source Generator auto-discovers IScheduleHandler<T> implementations

// Option B: Manual registration (100% AOT-safe)
services.AddScheduleHandler<CleanupHandler, CleanupJobData>();
```

### 3. Auto-Start at Application Startup

For schedules that should run on a fixed cadence and start the moment the application boots (no manual `IScheduleProvider.Start(...)` call), use `AddSchedule<THandler>(opts => ...)` with a handler implementing the no-data `IScheduleHandler` interface. The library ships an `IHostedService` that wires every registration during `StartAsync`, so you do not need to write your own host class.

```csharp
public sealed class DailyRadarSchedule : IScheduleHandler
{
    private readonly IRadarBuilder _builder;

    public DailyRadarSchedule(IRadarBuilder builder) => _builder = builder;

    public Task HandleAsync(CancellationToken cancellationToken)
        => _builder.BuildAsync(cancellationToken);
}

// Composition root - register once during startup
if (configuration.GetValue<bool>("Content:Enabled"))
{
    services.AddSchedule<DailyRadarSchedule>(o =>
    {
        o.Name = "Content.DailyRadar";
        o.Timeout = TimeSpan.FromMinutes(30);
        o.Schedules =
        [
            Schedule.Now(),                                  // run once at startup
            Schedule.EveryWeek(DayOfWeek.Tuesday, 6, 0),
            Schedule.EveryWeek(DayOfWeek.Wednesday, 6, 0),
            Schedule.EveryWeek(DayOfWeek.Thursday, 6, 0),
            Schedule.EveryWeek(DayOfWeek.Friday, 6, 0),
        ];
    });
}
```

The handler is registered as Scoped, so each invocation gets a fresh DI scope (safe for `DbContext`, scoped repositories, ambient transactions, etc.).

> Use `IScheduleHandler<TData>` + `IScheduleProvider.Start(data, ...)` instead when each scheduled run needs a different payload (database row id, tenant id, etc.).

### 4. Start Schedules

```csharp
public class MyService
{
    private readonly IScheduleProvider _scheduleProvider;

    public MyService(IScheduleProvider scheduleProvider)
    {
        _scheduleProvider = scheduleProvider;
    }

    public void StartJobs()
    {
        // Every 5 minutes
        var id1 = _scheduleProvider.Start(
            new CleanupJobData("/tmp", 7),
            Schedule.EveryMinutes(5)
        );

        // Daily at 3:00 AM
        var id2 = _scheduleProvider.Start(
            new CleanupJobData("/logs", 30),
            Schedule.EveryDay(3, 0)
        );

        // Multiple schedules (8:00 AM and 6:00 PM)
        var id3 = _scheduleProvider.Start(
            new CleanupJobData("/cache", 1),
            Schedule.EveryDay(8, 0),
            Schedule.EveryDay(18, 0)
        );

        // With options
        var id4 = _scheduleProvider.Start(
            new CleanupJobData("/data", 14),
            options =>
            {
                options.Name = "DataCleanup";
                options.Schedules = [Schedule.EveryHour(atMinute: 30)];
                options.ExecuteOnStartup = true;
            }
        );
    }
}
```

### 5. Managing Schedules

```csharp
// Pause/Resume
_scheduleProvider.Pause(id);
_scheduleProvider.Resume(id);

// Stop permanently
_scheduleProvider.Stop(id);

// Trigger immediate execution
_scheduleProvider.TriggerNow(id);

// Get state
var state = _scheduleProvider.GetState(id);
Console.WriteLine($"Status: {state?.Status}");
Console.WriteLine($"Last run: {state?.LastExecutionAt}");
Console.WriteLine($"Next run: {state?.NextExecutionAt}");
Console.WriteLine($"Executions: {state?.ExecutionCount}");

// Get all schedules
var all = _scheduleProvider.GetAllSchedules();
var active = _scheduleProvider.GetActiveSchedules();
```

### 6. Monitor State Changes

```csharp
// Subscribe to all schedule changes
_scheduleProvider.OnChange(state =>
{
    Console.WriteLine($"{state.Name}: {state.Status} ({state.ExecutionCount} runs)");
});

// Subscribe to specific schedule
_scheduleProvider.OnChange(scheduleId, state =>
{
    if (state.Status == ScheduleStatus.Failed)
        AlertAdmin(state.LastError);
});
```

### 7. Schedule Factory Methods

| Method | Description | Example |
|--------|-------------|---------|
| `Now` | Run once immediately | `Schedule.Now()` |
| `EverySeconds(n)` | Every N seconds | `Schedule.EverySeconds(30)` |
| `EveryMinutes(n)` | Every N minutes | `Schedule.EveryMinutes(5)` |
| `EveryHours(n)` | Every N hours | `Schedule.EveryHours(2)` |
| `EveryDays(n)` | Every N days | `Schedule.EveryDays(1)` |
| `EveryMinute()` | Every minute at :00 | `Schedule.EveryMinute()` |
| `EveryHour(min)` | Every hour at minute | `Schedule.EveryHour(30)` |
| `EveryDay(h, m)` | Daily at time | `Schedule.EveryDay(15, 0)` |
| `EveryWeek(day, h, m)` | Weekly on day | `Schedule.EveryWeek(DayOfWeek.Monday, 9, 0)` |
| `EveryMonth(day, h, m)` | Monthly on day | `Schedule.EveryMonth(1, 0, 0)` |
| `EveryYear(mo, d, h, m)` | Yearly on date | `Schedule.EveryYear(1, 1, 0, 0)` |

### 8. ScheduleState Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `ScheduleId` | Unique schedule identifier |
| `Name` | `string` | Schedule name |
| `Status` | `ScheduleStatus` | Pending, Running, Paused, Stopped, Completed, Failed |
| `Schedules` | `Schedule[]` | Schedule rules |
| `CreatedAt` | `DateTimeOffset` | When schedule was created |
| `LastExecutionAt` | `DateTimeOffset?` | When last execution occurred |
| `NextExecutionAt` | `DateTimeOffset?` | When next execution is scheduled |
| `ExecutionCount` | `int` | Total number of executions |
| `ConsecutiveFailures` | `int` | Number of consecutive failures |
| `LastError` | `string?` | Last error message |
| `LastExecutionDuration` | `TimeSpan?` | Duration of last execution |

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
