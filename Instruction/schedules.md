# Schedules (recurring jobs)

Run work on a recurring, cron-like schedule built from the strongly-typed `Schedule` value object
(compile-time safe, no cron strings). Each run executes in a fresh DI scope, and a schedule never
overlaps itself — if a run is still going when the next tick arrives, the next run waits.

## Fixed-cadence handler

For a job that runs on a fixed schedule and starts with the app, implement `IScheduleHandler` and
register it with `AddSchedule<THandler>`:

```csharp
public sealed class DailyCleanup : IScheduleHandler
{
    private readonly IStorage _storage;
    public DailyCleanup(IStorage storage) => _storage = storage;

    public Task HandleAsync(CancellationToken cancellationToken)
        => _storage.PurgeOldAsync(cancellationToken);
}

// Composition root:
services.AddScheduleHandlers();           // core scheduling services
services.AddSchedule<DailyCleanup>(o =>
{
    o.Name = "Storage.DailyCleanup";
    o.Timeout = TimeSpan.FromMinutes(30);
    o.Schedules =
    [
        Schedule.Now(),                       // once at startup
        Schedule.EveryDay(3, 0),              // and daily at 03:00
    ];
});
```

The handler is **scoped** — each invocation gets a fresh scope, so `DbContext`, scoped
repositories and ambient transactions are safe.

## Data-driven handler

When each run needs a different payload (a row id, a tenant), implement `IScheduleHandler<TData>`
and start runs dynamically through `IScheduleProvider`:

```csharp
public record CleanupJob(string Directory, int RetentionDays);

public sealed class CleanupHandler : IScheduleHandler<CleanupJob>
{
    public Task HandleAsync(CleanupJob data, CancellationToken ct) => /* ... */;
}

// register the handler (source-generated discovery), then start runs:
services.AddScheduleHandler<CleanupHandler, CleanupJob>();

ScheduleId id = scheduleProvider.Start(
    new CleanupJob("/tmp", 7),
    Schedule.EveryMinutes(5));
```

## The `Schedule` factory

| Method | Example |
| :--- | :--- |
| `Now()` | run once immediately |
| `EverySeconds(n)` / `EveryMinutes(n)` / `EveryHours(n)` / `EveryDays(n)` | `Schedule.EveryMinutes(5)` |
| `EveryMinute()` / `EveryHour(atMinute)` | `Schedule.EveryHour(30)` |
| `EveryDay(h, m)` | `Schedule.EveryDay(3, 0)` |
| `EveryWeek(day, h, m)` | `Schedule.EveryWeek(DayOfWeek.Monday, 9, 0)` |
| `EveryMonth(day, h, m)` / `EveryYear(mo, d, h, m)` | `Schedule.EveryMonth(1, 0, 0)` |

Pass several rules to run at multiple times; the earliest next occurrence across all rules wins.

## Manage and observe

```csharp
scheduleProvider.Pause(id);
scheduleProvider.Resume(id);
scheduleProvider.Stop(id);
scheduleProvider.TriggerNow(id);

var state = scheduleProvider.GetState(id);     // Status, LastExecutionAt, NextExecutionAt, ExecutionCount...
scheduleProvider.OnChange(id, s =>
{
    if (s.Status == ScheduleStatus.Failed) Alert(s.LastError);
});
```

## Failure handling

On an exception the run is recorded as a failure and the schedule keeps trying on its next
occurrence, with **backoff** that grows after consecutive failures (capped at 5 min) so a fast,
perpetually-failing schedule can't become a tight retry/log storm. To stop after a fixed number of
consecutive failures, set `MaxRetries` **and** `StopOnMaxRetries = true`.
