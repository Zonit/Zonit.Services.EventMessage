# Zonit.Messaging.Schedules

A strongly-typed scheduling system for recurring jobs with AOT/trimming support.

## Features

- **Strongly-typed schedules** - No string-based cron expressions
- **Binary storage** - Compact 16-byte format for database persistence
- **ExtensionId support** - Associate schedules with your domain entities
- **AOT-compatible** - Source Generator for handler discovery
- **Simple handler API** - Matches Events pattern: `(TData data, CancellationToken ct)`

## Installation

```csharp
// Register schedule services
services.AddScheduleServices();
```

The Source Generator automatically discovers and registers all `IScheduleHandler<T>` implementations.

## Quick Start

### 1. Define your data model

```csharp
public record DataFetchJobData(
    Guid RecordId,
    string Url,
    string TargetPath
);
```

### 2. Create a handler

```csharp
public class DataFetchHandler : IScheduleHandler<DataFetchJobData>
{
    private readonly ILogger<DataFetchHandler> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public DataFetchHandler(ILogger<DataFetchHandler> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task HandleAsync(DataFetchJobData data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching data for record {RecordId} from {Url}", 
            data.RecordId, data.Url);

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetStringAsync(data.Url, cancellationToken);
        
        // Process the data...
    }
}
```

### 3. Schedule jobs

```csharp
public class DataFetchService
{
    private readonly IScheduleProvider _scheduleProvider;

    public DataFetchService(IScheduleProvider scheduleProvider)
    {
        _scheduleProvider = scheduleProvider;
    }

    public ScheduleId ScheduleDailyFetch(Guid recordId, string url, int hour)
    {
        var data = new DataFetchJobData(recordId, url, "/data");
        
        // Schedule daily at specified hour, with ExtensionId for lookup
        return _scheduleProvider.Start(
            data,
            extensionId: recordId,  // Link to your domain entity
            Schedule.EveryDay(hour, 0)
        );
    }

    public void CancelFetchForRecord(Guid recordId)
    {
        // Find schedule by ExtensionId
        var state = _scheduleProvider.FindByExtensionId(recordId);
        if (state is not null)
        {
            _scheduleProvider.Stop(state.Id);
        }
    }
}
```

## Schedule Types

### Interval Mode

Execute at fixed intervals:

```csharp
Schedule.EveryMinutes(5)      // Every 5 minutes
Schedule.EveryMinutes(30)     // Every 30 minutes
Schedule.EveryHours(2)        // Every 2 hours
```

### Calendar Mode

Execute at specific times:

```csharp
Schedule.EveryDay(15, 0)                      // Daily at 15:00
Schedule.EveryDay(8, 30)                      // Daily at 08:30
Schedule.EveryWeek(DayOfWeek.Monday, 9, 0)    // Every Monday at 09:00
Schedule.EveryMonth(1, 0, 0)                  // 1st of each month at midnight
Schedule.EveryMonth(15, 12, 0)                // 15th of each month at noon
```

### Multiple Schedules

```csharp
// Execute at both 8:00 and 18:00
_scheduleProvider.Start(data, extensionId, 
    Schedule.EveryDay(8, 0), 
    Schedule.EveryDay(18, 0)
);
```

## ExtensionId - Linking to Domain Entities

The `ExtensionId` allows you to associate schedules with your domain entities (database records, user settings, etc.):

```csharp
// When creating a schedule
var scheduleId = _scheduleProvider.Start(
    data,
    extensionId: myDatabaseRecordId,  // Your domain ID
    Schedule.EveryDay(18, 0)
);

// Later - find by your domain ID
var state = _scheduleProvider.FindByExtensionId(myDatabaseRecordId);
if (state is not null)
{
    Console.WriteLine($"Found schedule: {state.Id}, executions: {state.ExecutionCount}");
}

// Or find all schedules for a domain entity
var allStates = _scheduleProvider.FindAllByExtensionId(myDatabaseRecordId);
```

### Use Cases

- **User-configured jobs**: User sets up daily data sync at 18:00 → `ExtensionId = userId`
- **Record-based schedules**: Each database record has its own schedule → `ExtensionId = recordId`
- **Feature-based grouping**: Group schedules by feature → `ExtensionId = featureGuid`

## API Reference

### IScheduleProvider

| Method | Description |
|--------|-------------|
| `Start<TData>(data, schedules)` | Start a new schedule |
| `Start<TData>(data, extensionId, schedules)` | Start with ExtensionId for lookup |
| `Stop(scheduleId)` | Stop and remove a schedule |
| `Pause(scheduleId)` | Pause execution (keeps state) |
| `Resume(scheduleId)` | Resume paused schedule |
| `TriggerNow(scheduleId)` | Trigger immediate execution |
| `GetState(scheduleId)` | Get current schedule state |
| `GetAllSchedules()` | Get all schedule states |
| `GetActiveSchedules()` | Get running/paused schedules |
| `FindByExtensionId(extensionId)` | Find first schedule state by ExtensionId |
| `FindAllByExtensionId(extensionId)` | Find all schedule states by ExtensionId |
| `OnChange(handler)` | Subscribe to state changes |

### IScheduleHandler<TData>

```csharp
public interface IScheduleHandler<TData> where TData : notnull
{
    Task HandleAsync(TData data, CancellationToken cancellationToken);
}
```

### ScheduleState

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `ScheduleId` | Unique schedule identifier |
| `ExtensionId` | `Guid?` | Optional user-defined identifier |
| `Status` | `ScheduleStatus` | Running, Paused, Stopped |
| `Schedules` | `Schedule[]` | Active schedules |
| `NextExecutionAt` | `DateTimeOffset?` | Next planned execution |
| `LastExecutionAt` | `DateTimeOffset?` | Last execution time |
| `ExecutionCount` | `int` | Total executions |

## Source Generator

The `Zonit.Messaging.Schedules.SourceGenerators` package automatically discovers handlers at compile-time for AOT compatibility:

```csharp
// Handler is automatically registered - no manual DI setup needed
public class MyHandler : IScheduleHandler<MyData>
{
    public Task HandleAsync(MyData data, CancellationToken cancellationToken)
    {
        // Process data...
        return Task.CompletedTask;
    }
}
```

## Simple Background Tasks (AddSchedule)

For simple recurring tasks that don't require data, use `AddSchedule<THandler>`:

### 1. Create a simple handler

```csharp
public class CleanupHandler : IScheduleHandler
{
    private readonly ILogger<CleanupHandler> _logger;
    private readonly IDbContext _db;

    public CleanupHandler(ILogger<CleanupHandler> logger, IDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running cleanup...");
        
        // Delete old records
        await _db.DeleteOldRecordsAsync(cancellationToken);
    }
}
```

### 2. Register at startup

```csharp
// In Program.cs or Startup.cs
services.AddSchedule<CleanupHandler>(Schedule.EveryMinutes(30));

// With options
services.AddSchedule<ReportHandler>(options =>
{
    options.Name = "Daily Report";
    options.Schedules = [Schedule.EveryDay(6, 0)];  // Daily at 6:00 AM
    options.ExecuteOnStartup = true;  // Run immediately when app starts
});

// Multiple schedules
services.AddSchedule<SyncHandler>(
    Schedule.EveryDay(8, 0),   // Morning sync
    Schedule.EveryDay(18, 0)   // Evening sync
);
```

### When to use AddSchedule vs IScheduleProvider

| Scenario | Use |
|----------|-----|
| Simple recurring task (cleanup, health check) | `AddSchedule<THandler>` |
| Static schedule defined at startup | `AddSchedule<THandler>` |
| Dynamic schedules created at runtime | `IScheduleProvider.Start()` |
| Passing data to handler | `IScheduleProvider.Start()` |
| User-configurable schedules | `IScheduleProvider.Start()` |
| Need to stop/pause/resume | `IScheduleProvider.Start()` |

## Binary Storage

The `Schedule` ValueObject uses compact 16-byte binary format:

```csharp
// Convert to bytes for database storage
byte[] bytes = schedule.ToBytes();

// Restore from bytes
Schedule restored = Schedule.FromBytes(bytes);
```

See [Schedule.README.md](../../../Extensions/Zonit.Extensions/Source/Zonit.Extensions/ValueObjects/Schedule.README.md) for detailed Schedule ValueObject documentation.
