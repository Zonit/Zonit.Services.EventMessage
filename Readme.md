# Event Message Service

## Overview

**Event Message Service** is a powerful .NET library that enables event-driven architecture for .NET 8/9 applications. It allows seamless, decoupled communication between application components via events, resulting in maintainable and flexible codebases.

---

## :package: NuGet Packages

### Abstractions

```powershell
Install-Package Zonit.Services.EventMessage.Abstractions 
```
![NuGet Version](https://img.shields.io/nuget/v/Zonit.Services.EventMessage.Abstractions.svg)
![NuGet Downloads](https://img.shields.io/nuget/dt/Zonit.Services.EventMessage.Abstractions.svg)

### Implementation

```powershell
Install-Package Zonit.Services.EventMessage.SqlServer
```
![NuGet Version](https://img.shields.io/nuget/v/Zonit.Services.EventMessage.svg)
![NuGet Downloads](https://img.shields.io/nuget/dt/Zonit.Services.EventMessage.svg)

---

## Features

- **Event Publishing & Subscription:** Publish and subscribe to events with configurable concurrency control.
- **Transaction Support:** Group events into transactions to be processed sequentially.
- **Task Management:** Handle long-running tasks with status tracking and monitoring.
- **Automatic Handler Discovery:** Automatically discover and register event handlers.
- **Concurrent Processing:** Control the number of concurrently executed event handlers.
- **Timeout Handling:** Configure timeouts for event processing.

---

## Requirements

- .NET 8 or .NET 9

---

## Installation

Add the Event Message Service to your application using the service collection extension:

```csharp
services.AddEventMessageService();
```

---

## Usage

### 1. Creating Event Handlers

Implement event handlers by inheriting from `EventBase<T>`, where `T` is your event model type:

```csharp
internal class Test1Event(ILogger<Test1Event> _logger) : EventBase<Test1Model>
{
    protected override async Task HandleAsync(Test1Model payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        _logger.LogInformation("[TestEvent] Number: {number} Title: {title}", 1, payload);
    }
}
```

---

### 2. Publishing Events

Publish events using the `IEventProvider` interface:

```csharp
var eventProvider = serviceProvider.GetRequiredService<IEventProvider>();
eventProvider.Publish(new Test1Model { Title = "Test" });
```

---

### 3. Using Transactions

Group events to be processed sequentially within a transaction:

```csharp
using (var transaction = eventProvider.Transaction())
{
    eventProvider.Publish(new Test1Model { Title = "Test1" });
    eventProvider.Publish(new Test2Model { Title = "Test2" });
    // Events are queued until the transaction is completed
}
// Events are processed after the transaction is disposed
```

---

### 4. Awaiting Transaction Completion

Wait for all events in a transaction to be processed:

```csharp
using (var transaction = eventProvider.Transaction())
{
    eventProvider.Publish(new Test1Model { Title = "Test" });

    // Wait for all events to be processed
    await transaction.WaitForCompletionAsync();
}
```

---

## Task Management System

The Event Message Service includes a comprehensive task management system for handling long-running operations with status tracking and monitoring.

### 1. Creating Task Handlers

Implement task handlers by inheriting from `TaskBase<T>`, where `T` is your task model:

```csharp
internal class TestTask(ILogger<TestTask> _logger) : TaskBase<TestTaskModel>
{
    protected override async Task HandleAsync(TestTaskModel payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        _logger.LogInformation("[TestTask] Title: {title}", payload.Title);
    }
}
```

---

### 2. Publishing Tasks

Submit tasks to the queue using the `ITaskProvider` interface:

```csharp
var taskProvider = serviceProvider.GetRequiredService<ITaskProvider>();
taskProvider.Publish(new TestTaskModel { Title = "Test Task" });
```

---

### 3. Monitoring Task Status

Subscribe to task status change events:

```csharp
taskProvider.TaskStatusChanged += (sender, args) =>
{
    _logger.LogInformation("Task {taskId} status changed to {status}", args.TaskId, args.Status);
};
```

---

### 4. Viewing Active Tasks

Retrieve and display the list of active tasks:

```csharp
var activeTasks = taskProvider.GetActiveTasks();
foreach (var task in activeTasks)
{
    _logger.LogInformation("Active Task: {taskId} - {status}", task.Id, task.Status);
}
```

---

### 5. Task Lifecycle Management

Tasks go through various states during their lifecycle:

- `Pending`: Task is queued but not yet started.
- `Processing`: Task is currently being processed.
- `Completed`: Task finished successfully.
- `Failed`: Task failed during processing.
- `Cancelled`: Task was cancelled before completion.

---

## Example Use Cases

- Decoupling logic between independent application components
- Implementing event-driven workflows
- Handling system notifications and real-time updates
- Creating robust background job queues

---

## Contributing & Support

Found a bug or have a feature request? Open an [issue](https://github.com/Zonit/Zonit.Services.EventMessage/issues/new) on GitHub!

---

## License

[MIT](LICENSE)
