# Tasks (background jobs)

A task is queued work that runs on background workers with optional **progress reporting**,
**retries**, and **live state** you can observe from a UI. Like events, publishing is
fire-and-forget into an in-process channel.

## Define a handler

Derive from `TaskHandler<TTask>`. Override the protected `HandleAsync`; optionally declare steps
for smooth progress and tune workers/timeout/retries:

```csharp
public record ImportData(string Source, int RecordCount);

public sealed class ImportDataHandler : TaskHandler<ImportData>
{
    public override int WorkerCount => 2;
    public override TimeSpan Timeout => TimeSpan.FromMinutes(10);
    public override int MaxRetries => 3;
    public override string? Title => "Import data";

    // Estimated step durations drive smooth, time-based progress.
    public override TaskProgressStep[]? ProgressSteps =>
    [
        new(TimeSpan.FromSeconds(5),  "Connecting..."),
        new(TimeSpan.FromSeconds(15), "Downloading..."),
        new(TimeSpan.FromSeconds(20), "Saving..."),
    ];

    protected override async Task HandleAsync(
        ImportData data, ITaskProgressContext progress, CancellationToken cancellationToken)
    {
        await progress.NextAsync();                 // -> step 1
        await ConnectAsync(cancellationToken);

        await progress.NextAsync();                 // -> step 2
        for (int i = 0; i < data.RecordCount; i++)
            await progress.SetMessageAsync($"Row {i + 1}/{data.RecordCount}");

        await progress.NextAsync();                 // -> step 3
        await SaveAsync(cancellationToken);
    }
}
```

Register with `services.AddTaskHandlers();`. For a handler that doesn't need progress, implement
`ITaskHandler<TTask>` directly.

### Overridable settings on `TaskHandler<T>`

| Member | Default | Meaning |
|:---|:---|:---|
| `WorkerCount` | `10` | Parallel workers for this task type |
| `Timeout` | `5 min` | Per-execution limit (`Timeout.InfiniteTimeSpan` to disable) |
| `MaxRetries` | `0` | Re-attempts after a failure, spaced by `RetryDelay` |
| `RetryDelay` | `5 s` | Delay between retries |
| `ContinueOnError` | `true` | Keep draining the queue after a terminal failure |
| `Title` / `Description` | `null` | Shown in `TaskState` for UIs |
| `ProgressSteps` | `null` | Step durations + messages for smooth progress |

## The progress context

`ITaskProgressContext` (the `progress` parameter) drives the reported percentage:

| Member | Purpose |
|:---|:---|
| `Task NextAsync(string? message = null)` | advance to the next step |
| `Task GoToAsync(int stepIndex, string? message = null)` | jump to a 0-based step |
| `Task SetMessageAsync(string message)` | change the message without changing the step |
| `Task SetProgressAsync(int percentage, string? message = null)` | set an explicit % (for handlers without steps) |
| `int CurrentStepIndex` / `int CurrentProgress` / `int TotalSteps` | current state |

Updates are throttled to whole-percent changes (≈100 updates per task), so reporting frequently
inside a loop is cheap.

## Publish

Resolve `ITaskProvider`. The optional `extensionId` is a correlation key for filtering (e.g. an
organization id), **not** an authorization boundary:

```csharp
taskProvider.Publish(new ImportData("data.csv", 1000));
taskProvider.Publish(new ImportData("org.csv", 5000), organizationId);
```

## Observe progress and state

Resolve `ITaskManager`. Subscribe to live updates or query active tasks. `OnChange` returns an
`IDisposable`; dispose it to unsubscribe. Subscribers receive an immutable snapshot of the state.

```csharp
// All tasks
using var sub = taskManager.OnChange(s =>
    Console.WriteLine($"{s.TaskType}: {s.Progress}% {s.Message}"));

// Typed access for one type
taskManager.OnChange<ImportData>(s =>
    Console.WriteLine($"Importing {s.Data.Source}: {s.Progress}% (step {s.CurrentStep}/{s.TotalSteps})"));

// Up to four types in one handler, or filtered by extension id
taskManager.OnChange<ImportData, ExportData>(s => Notify(s));
taskManager.OnChange(organizationId, s => UpdateBar(s.Progress ?? 0));
taskManager.OnChange<ImportData>(organizationId, s => UpdateUi(s.Data, s.Progress));

// Snapshot the current set
IReadOnlyCollection<TaskState> active = taskManager.GetActiveTasks();
IReadOnlyCollection<TaskState<ImportData>> orgImports = taskManager.GetActiveTasks<ImportData>(organizationId);
TaskState? one = taskManager.GetTaskState(taskId);
```

Completed/failed/cancelled tasks are evicted automatically ~30 minutes after they finish.

## Lifecycle

`Status` moves through `Pending` → `Processing` → `Completed` | `Failed` | `Cancelled`. A task
that throws is retried up to `MaxRetries` (spaced by `RetryDelay`); after the last attempt it is
marked `Failed`. A `Timeout` cancels the handler token and counts as a retryable failure.

## Manual registration / ad-hoc subscription

Auto-discovery (`AddTaskHandlers()`) is the norm. You can also register one handler explicitly, or
subscribe a delegate (which lets you pass `TaskSubscriptionOptions` directly):

```csharp
services.AddTask<ImportDataHandler, ImportData>();

taskManager.Subscribe<ImportData>(async payload =>
{
    await Import(payload.Data, payload.CancellationToken);
}, new TaskSubscriptionOptions { WorkerCount = 5, MaxRetries = 3, Capacity = 10_000 });
```

## API reference

### `TaskState` properties (`Zonit.Messaging.Tasks.Abstractions`)

| Property | Type | Meaning |
|:---|:---|:---|
| `TaskId` | `Guid` | unique id |
| `ExtensionId` | `Guid?` | correlation key (not a security boundary) |
| `TaskType` | `string` | full type name of the task |
| `Title` / `Description` | `string?` | optional display fields |
| `Status` | `TaskStatus` | `Pending`/`Processing`/`Completed`/`Failed`/`Cancelled` |
| `Progress` | `int?` | 0-100, or null if untracked |
| `CurrentStep` / `TotalSteps` | `int?` | step counters (1-based current) |
| `Message` | `string?` | current status message |
| `CreatedAt` / `StartedAt` / `CompletedAt` | `DateTimeOffset(?)` | timestamps |
| `Duration` | `TimeSpan?` | elapsed since `StartedAt` |
| `TaskState<TTask>.Data` | `TTask` | typed payload (via `OnChange<T>` / `GetActiveTasks<T>`) |

### `ITaskManager` methods

| Method | Returns | Purpose |
|:---|:---|:---|
| `Publish<TTask>(payload, extensionId?)` | `void` | enqueue a task |
| `Subscribe<TTask>(Func<TaskPayload<TTask>, Task>, TaskSubscriptionOptions?)` | `void` | delegate handler |
| `OnChange(Action<TaskState>)` | `IDisposable` | all changes |
| `OnChange(Guid, Action<TaskState>)` | `IDisposable` | by extension id |
| `OnChange<TTask>(Action<TaskState<TTask>>)` | `IDisposable` | typed |
| `OnChange<TTask>(Guid, Action<TaskState<TTask>>)` | `IDisposable` | typed + extension id |
| `OnChange<T1..T4>(Action<TaskState>)` | `IDisposable` | 2-4 types at once |
| `GetActiveTasks(extensionId?)` | `IReadOnlyCollection<TaskState>` | active (Pending/Processing) |
| `GetActiveTasks<TTask>(extensionId?)` | `IReadOnlyCollection<TaskState<TTask>>` | typed |
| `GetActiveTasks<T1..T4>(extensionId?)` | `IReadOnlyCollection<TaskState>` | 2-4 types |
| `GetTaskState(Guid)` | `TaskState?` | one task by id |

### `TaskSubscriptionOptions`

`WorkerCount` (10), `Timeout` (5 min), `ContinueOnError` (true), `MaxRetries` (0),
`RetryDelay` (5 s), `ProgressSteps`, `Title`, `Description`, `Capacity` (null = unbounded).

### Registration (`Zonit.Messaging.Tasks`)

| Method | Purpose |
|:---|:---|
| `AddTaskHandlers()` | core services + all source-generated handlers (call once per assembly) |
| `AddTask<THandler, TTask>()` / `(Action<TaskSubscriptionOptions>)` | manually register one handler (AOT-safe) |
