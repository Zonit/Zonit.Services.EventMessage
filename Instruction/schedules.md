# Schedules (recurring jobs)

Run work on a recurring, cron-like schedule built from the strongly-typed `Schedule` value object
(compile-time safe, no cron strings). Each run executes in a fresh DI scope, and a schedule never
overlaps itself: if a run is still going when the next tick arrives, the next run waits.

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

There is also a shorthand: `services.AddSchedule<DailyCleanup>(Schedule.EveryMinutes(10));`.
The handler is **scoped**, so each invocation gets a fresh scope and `DbContext`, scoped
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

// register the handler (source-generated discovery)
services.AddScheduleHandler<CleanupHandler, CleanupJob>();

// then start runs (returns a ScheduleId you can manage later):
ScheduleId id = scheduleProvider.Start(new CleanupJob("/tmp", 7), Schedule.EveryMinutes(5));
ScheduleId id2 = scheduleProvider.Start(new CleanupJob("/logs", 30), tenantId, Schedule.EveryDay(2, 0));
ScheduleId id3 = scheduleProvider.Start(new CleanupJob("/cache", 1), o =>
{
    o.Name = "CacheCleanup";
    o.Schedules = [Schedule.EveryHour(atMinute: 30)];
    o.ExecuteOnStartup = true;
});
```

## The `Schedule` factory

| Method | Example |
|:---|:---|
| `Now()` | run once immediately |
| `EverySeconds(n)` / `EveryMinutes(n)` / `EveryHours(n)` / `EveryDays(n)` | `Schedule.EveryMinutes(5)` |
| `EveryMinute()` / `EveryHour(atMinute)` | `Schedule.EveryHour(30)` |
| `EveryDay(h, m)` | `Schedule.EveryDay(3, 0)` |
| `EveryWeek(day, h, m)` | `Schedule.EveryWeek(DayOfWeek.Monday, 9, 0)` |
| `EveryMonth(day, h, m)` | `Schedule.EveryMonth(1, 0, 0)` |
| `EveryYear(month, day, h, m)` | `Schedule.EveryYear(1, 1, 0, 0)` |

Pass several rules to run at multiple times; the earliest next occurrence across all rules wins.

## Manage and observe

```csharp
scheduleProvider.Pause(id);
scheduleProvider.Resume(id);
scheduleProvider.Stop(id);
scheduleProvider.TriggerNow(id);

ScheduleState? state = scheduleProvider.GetState(id);
foreach (var s in scheduleProvider.GetActiveSchedules()) { /* Running or Paused */ }

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

## API reference

### `Schedule` options (`ScheduleOptions`)

| Property | Default | Meaning |
|:---|:---|:---|
| `Schedules` | `[]` (required) | one or more `Schedule` rules |
| `ExecuteOnStartup` | `false` | run once at startup |
| `Timeout` | `5 min` | per-execution limit |
| `MaxRetries` | `0` | consecutive-failure threshold (with `StopOnMaxRetries`) |
| `RetryDelay` | `30 s` | base for failure backoff |
| `StopOnMaxRetries` | `false` | stop (Failed) after `MaxRetries` consecutive failures |
| `Name` | type name | shown in logs/monitoring |
| `Description` | `null` | optional |
| `TimeZone` | local | for calendar-based schedules |
| `ExtensionId` | `null` | correlation key for `FindByExtensionId` |

### `ScheduleState` properties

`Id` (`ScheduleId`), `ExtensionId`, `Name`, `Status` (`ScheduleStatus`), `Schedules`,
`CreatedAt`, `LastExecutionAt`, `NextExecutionAt`, `ExecutionCount`, `ConsecutiveFailures`,
`LastError`, `LastExecutionDuration`, `Description`. `ScheduleStatus` =
`Pending`/`Running`/`Paused`/`Stopped`/`Completed`/`Failed`.

### `IScheduleProvider` methods

| Method | Returns | Purpose |
|:---|:---|:---|
| `Start<TData>(data, params Schedule[])` | `ScheduleId` | start a dynamic schedule |
| `Start<TData>(data, Guid extensionId, params Schedule[])` | `ScheduleId` | with a correlation id |
| `Start<TData>(data, Action<ScheduleOptions>)` | `ScheduleId` | with full options |
| `Stop` / `Pause` / `Resume` / `TriggerNow` | `bool` | manage by id |
| `GetState(id)` | `ScheduleState?` | one schedule |
| `FindByExtensionId(Guid)` / `FindAllByExtensionId(Guid)` | `ScheduleState?` / collection | look up by correlation id |
| `GetAllSchedules()` / `GetActiveSchedules()` | `IReadOnlyCollection<ScheduleState>` | list all / Running+Paused |
| `OnChange(Action<ScheduleState>)` / `OnChange(ScheduleId, Action<ScheduleState>)` | `IDisposable` | subscribe to changes |

### Registration (`Zonit.Messaging.Schedules`)

| Method | Purpose |
|:---|:---|
| `AddScheduleHandlers()` (alias of `AddScheduleServices()`) | core services + source-generated handlers |
| `AddSchedule<THandler>(params Schedule[])` / `(Action<ScheduleOptions>)` | register + auto-start an `IScheduleHandler` |
| `AddScheduleHandler<THandler, TData>()` | register an `IScheduleHandler<TData>` for use with `IScheduleProvider.Start` |
