# Tasks (background jobs)

A task is queued work that runs on background workers with optional **progress reporting**,
**retries**, and **live state** you can observe from a UI. Like events, publishing is
fire-and-forget into an in-process channel.

## Define a handler

Derive from `TaskHandler<TTask>`. Override the protected `HandleAsync`; optionally declare steps
for smooth progress and tune workers/timeout:

```csharp
public record ImportData(string Source, int RecordCount);

public sealed class ImportDataHandler : TaskHandler<ImportData>
{
    public override int WorkerCount => 2;
    public override TimeSpan Timeout => TimeSpan.FromMinutes(10);
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

| Property | Default | Meaning |
| :--- | :--- | :--- |
| `WorkerCount` | `10` | Parallel workers for this task type |
| `Timeout` | `5 min` | Per-execution limit (`Timeout.InfiniteTimeSpan` to disable) |
| `MaxRetries` | `0` | Re-attempts after a failure, spaced by `RetryDelay` |
| `ContinueOnError` | `true` | Keep draining the queue after a terminal failure |

## Publish

Resolve `ITaskProvider`. The optional `extensionId` is a correlation key for filtering (e.g. an
organization id) — **not** an authorization boundary:

```csharp
taskProvider.Publish(new ImportData("data.csv", 1000));
taskProvider.Publish(new ImportData("org.csv", 5000), organizationId);
```

## Observe progress and state

Resolve `ITaskManager`. Subscribe to live updates or query active tasks. `OnChange` returns an
`IDisposable`; dispose it to unsubscribe. Subscribers receive an immutable snapshot of the state:

```csharp
// All tasks
using var sub = taskManager.OnChange(s =>
    Console.WriteLine($"{s.TaskType}: {s.Progress}% — {s.Message}"));

// Typed access for one type
taskManager.OnChange<ImportData>(s =>
    Console.WriteLine($"Importing {s.Data.Source}: {s.Progress}% (step {s.CurrentStep}/{s.TotalSteps})"));

// Up to four types, or filtered by extension id
taskManager.OnChange<ImportData, ExportData>(s => Notify(s));
taskManager.OnChange(organizationId, s => UpdateBar(s.Progress ?? 0));

// Snapshot the current set
var active = taskManager.GetActiveTasks();
var orgImports = taskManager.GetActiveTasks<ImportData>(organizationId);
var one = taskManager.GetTaskState(taskId);
```

`TaskState` carries `Status` (Pending/Processing/Completed/Failed/Cancelled), `Progress`,
`CurrentStep`/`TotalSteps`, `Message`, timestamps and `Duration`. Completed/failed/cancelled tasks
are evicted automatically ~30 minutes after they finish.

> Progress updates are throttled to whole-percent changes (≈100 updates per task), so reporting
> frequently inside a loop is cheap.
