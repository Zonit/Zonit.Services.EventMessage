using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Base class for schedule entries with timer management.
/// </summary>
internal abstract class ScheduleEntry : IDisposable
{
    private Timer? _timer;
    private readonly object _timerLock = new();
    // Cancelled once when the entry is disposed/stopped; every execution links its token to this.
    private readonly CancellationTokenSource _lifetimeCts = new();
    // Serializes executions so a schedule never overlaps itself (timer vs. TriggerNow vs. startup).
    private readonly SemaphoreSlim _executionGate = new(1, 1);
    private bool _disposed;

    public ScheduleId Id { get; }
    public ScheduleOptions Options { get; }
    public ScheduleState State { get; }

    protected ILogger Logger { get; }

    protected ScheduleEntry(ScheduleId id, ScheduleOptions options, ScheduleState state, ILogger logger)
    {
        Id = id;
        Options = options;
        State = state;
        Logger = logger;
    }

    public void ScheduleNext()
    {
        if (_disposed || State.Status != ScheduleStatus.Running)
            return;

        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? nextTime = null;

        // Find the earliest next occurrence from all schedules
        foreach (var schedule in Options.Schedules)
        {
            var next = schedule.GetNextOccurrence(now);
            if (next.HasValue && (!nextTime.HasValue || next.Value < nextTime.Value))
            {
                nextTime = next;
            }
        }

        if (!nextTime.HasValue)
        {
            // If all schedules are one-shot (e.g. Schedule.Now()), there is no next
            // occurrence and this is expected - don't warn.
            var hasRecurring = false;
            foreach (var s in Options.Schedules)
            {
                if (!s.IsNow || s.IsInterval || HasCalendar(s))
                {
                    hasRecurring = true;
                    break;
                }
            }

            if (hasRecurring)
                Logger.LogWarning("Schedule '{Name}' has no valid next occurrence", State.Name);
            else
                Logger.LogDebug("Schedule '{Name}' is one-shot (Now); not rescheduling", State.Name);

            return;
        }

        static bool HasCalendar(Schedule s) =>
            s.Second.HasValue || s.Minute.HasValue || s.Hour.HasValue ||
            s.DayOfMonth.HasValue || s.Month.HasValue || s.DayOfWeek.HasValue;

        State.NextExecutionAt = nextTime;
        var delay = nextTime.Value - now;

        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;

        // Cap at max timer delay (~49 days)
        if (delay > TimeSpan.FromDays(49))
        {
            delay = TimeSpan.FromDays(49);
            Logger.LogDebug("Schedule '{Name}' delay capped to 49 days", State.Name);
        }

        // Back off on consecutive failures so a fast, perpetually-failing schedule does not
        // turn into a tight retry/log storm. Grows with RetryDelay, capped at 5 minutes.
        if (State.ConsecutiveFailures > 0 && Options.RetryDelay > TimeSpan.Zero)
        {
            var shift = Math.Min(State.ConsecutiveFailures - 1, 6);
            var backoffTicks = Math.Min(
                Options.RetryDelay.Ticks * (1L << shift),
                TimeSpan.FromMinutes(5).Ticks);
            if (delay.Ticks < backoffTicks)
                delay = TimeSpan.FromTicks(backoffTicks);
        }

        lock (_timerLock)
        {
            _timer?.Dispose();
            _timer = new Timer(OnTimerCallback, null, delay, Timeout.InfiniteTimeSpan);
        }

        Logger.LogDebug("Schedule '{Name}' next execution at {NextTime}", State.Name, nextTime);
    }

    private async void OnTimerCallback(object? state)
    {
        if (_disposed) return;

        // Check if it's time to execute or if we need to reschedule
        var nextTime = State.NextExecutionAt;
        if (nextTime.HasValue && nextTime.Value > DateTimeOffset.UtcNow.AddSeconds(1))
        {
            // Not yet time, reschedule (for long delays)
            ScheduleNext();
            return;
        }

        await RunGuardedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Runs one execution, serialized so a schedule never overlaps itself. A trigger that
    /// arrives while a previous execution is still running waits for it to finish rather than
    /// starting a concurrent run. Each execution's token is linked to the entry's lifetime,
    /// so Dispose/Stop cancels in-flight work; the per-run linked source is always disposed.
    /// </summary>
    private async Task RunGuardedAsync()
    {
        try
        {
            await _executionGate.WaitAsync(_lifetimeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }
        catch (ObjectDisposedException) { return; }

        CancellationTokenSource? cts = null;
        try
        {
            if (_disposed) return;
            cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            await ExecuteAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error executing schedule '{Name}'", State.Name);
        }
        finally
        {
            cts?.Dispose();
            try { _executionGate.Release(); } catch (ObjectDisposedException) { }
        }
    }

    public void Pause()
    {
        lock (_timerLock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        UpdateStatus(ScheduleStatus.Paused);
    }

    public void Resume()
    {
        UpdateStatus(ScheduleStatus.Running);
        ScheduleNext();
    }

    public async void TriggerNow()
    {
        if (_disposed) return;
        await RunGuardedAsync().ConfigureAwait(false);
    }

    public void UpdateStatus(ScheduleStatus status)
    {
        State.Status = status;
    }

    public void RecordSuccess(DateTimeOffset executionTime)
    {
        State.LastExecutionAt = executionTime;
        State.ExecutionCount++;
        State.ConsecutiveFailures = 0;
        State.LastError = null;
    }

    public void RecordFailure(Exception ex)
    {
        State.ConsecutiveFailures++;
        State.LastError = ex.Message;

        // Only give up when StopOnMaxRetries is opted in. By default the schedule keeps
        // retrying on its next occurrence (with failure backoff applied in ScheduleNext).
        if (Options.StopOnMaxRetries && Options.MaxRetries > 0 &&
            State.ConsecutiveFailures >= Options.MaxRetries)
        {
            UpdateStatus(ScheduleStatus.Failed);
            Logger.LogError("Schedule '{Name}' exceeded max retries ({Max}); stopping (StopOnMaxRetries).",
                State.Name, Options.MaxRetries);
        }
    }

    protected abstract Task ExecuteAsync(CancellationToken cancellationToken);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _lifetimeCts.Cancel();

        lock (_timerLock)
        {
            _timer?.Dispose();
            _timer = null;
        }

        _lifetimeCts.Dispose();
        _executionGate.Dispose();
    }
}

/// <summary>
/// Typed schedule entry containing the data payload.
/// </summary>
internal sealed class ScheduleEntry<TData> : ScheduleEntry where TData : notnull
{
    private readonly Func<ScheduleEntry, CancellationToken, Task> _executeCallback;

    public TData Data { get; }

    public ScheduleEntry(
        ScheduleId id,
        TData data,
        ScheduleOptions options,
        ScheduleState state,
        Func<ScheduleEntry, CancellationToken, Task> executeCallback,
        ILogger logger)
        : base(id, options, state, logger)
    {
        Data = data;
        _executeCallback = executeCallback;
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        return _executeCallback(this, cancellationToken);
    }
}
