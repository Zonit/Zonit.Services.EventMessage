# Test

Test and benchmark projects for Zonit.Messaging. These live **outside** `Source/` so they are
never packed or published to NuGet.

| Project | What it is |
| :--- | :--- |
| `Zonit.Messaging.Tests` | xUnit unit/integration tests for Commands, Events, Tasks, Schedules and the transaction engine. They reference the real packages plus their source generators, so the AOT-safe generated registration is exercised end to end. |
| `Zonit.Messaging.Benchmarks` | BenchmarkDotNet micro-benchmarks (with `MemoryDiagnoser`) for the publish and dispatch hot paths. |

## Run the tests

```powershell
dotnet test Test/Zonit.Messaging.Tests/Zonit.Messaging.Tests.csproj
```

The suite covers, among others, the regressions fixed during the June 2026 audit:

- schedules never overlap themselves (`ScheduleTests.Schedule_never_overlaps_itself`)
- `StopOnMaxRetries` actually stops a failing schedule
- `EventTransaction` is thread-safe under concurrent ambient publishes and dispatches in order
- `WaitForCompletionAsync` commits-then-waits instead of being a silent no-op
- task-state subscribers get an immutable snapshot they cannot use to corrupt the store
- tasks retry up to `MaxRetries` and report progress to 100%

## Run the benchmarks

Always Release. Pick a benchmark with `--filter`, or run them all:

```powershell
dotnet run -c Release --project Test/Zonit.Messaging.Benchmarks -- --filter *EventPublish*
dotnet run -c Release --project Test/Zonit.Messaging.Benchmarks    # all
```

The benchmarks report time and **allocations per operation** for `EventPublish` (enqueue),
`CommandSend` (request/response), `TaskPublish` (enqueue + state) and `EventDispatch` (the full
per-message dispatch: DI scope + `GetServices` + handler). Use these numbers before optimizing the
dispatch path, rather than guessing.
