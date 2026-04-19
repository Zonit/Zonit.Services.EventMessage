using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zonit.Extensions;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Default implementation of IScheduleProvider.
/// Uses Timer-based scheduling with support for both interval and calendar-based schedules.
/// </summary>
public sealed class ScheduleManager : IScheduleProvider, IDisposable
{
    private readonly ConcurrentDictionary<ScheduleId, ScheduleEntry> _schedules = new();
    private readonly ConcurrentDictionary<ScheduleId, List<Action<ScheduleState>>> _stateHandlers = new();
    private readonly List<Action<ScheduleState>> _globalHandlers = [];
    private readonly object _handlersLock = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduleManager> _logger;
    private bool _disposed;

    public ScheduleManager(IServiceProvider serviceProvider, ILogger<ScheduleManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    #region IScheduleProvider

    public ScheduleId Start<TData>(TData data, params Schedule[] schedules) where TData : notnull
    {
        if (schedules.Length == 0)
            throw new ArgumentException("At least one schedule is required.", nameof(schedules));

        var options = new ScheduleOptions { Schedules = schedules };
        return StartInternal(data, options);
    }

    public ScheduleId Start<TData>(TData data, Guid extensionId, params Schedule[] schedules) where TData : notnull
    {
        if (schedules.Length == 0)
            throw new ArgumentException("At least one schedule is required.", nameof(schedules));

        var options = new ScheduleOptions { Schedules = schedules, ExtensionId = extensionId };
        return StartInternal(data, options);
    }

    public ScheduleId Start<TData>(TData data, Action<ScheduleOptions> configure) where TData : notnull
    {
        var options = new ScheduleOptions();
        configure(options);

        if (options.Schedules.Length == 0)
            throw new ArgumentException("At least one schedule is required.");

        return StartInternal(data, options);
    }

    private ScheduleId StartInternal<TData>(TData data, ScheduleOptions options) where TData : notnull
    {
        var id = ScheduleId.New();
        var name = options.Name ?? typeof(TData).Name;

        var state = new ScheduleState
        {
            Id = id,
            ExtensionId = options.ExtensionId,
            Name = name,
            Status = ScheduleStatus.Running,
            Schedules = options.Schedules,
            CreatedAt = DateTimeOffset.UtcNow,
            Description = options.Description
        };

        var entry = new ScheduleEntry<TData>(
            id: id,
            data: data,
            options: options,
            state: state,
            executeCallback: ExecuteScheduleAsync<TData>,
            logger: _logger
        );

        if (!_schedules.TryAdd(id, entry))
        {
            entry.Dispose();
            throw new InvalidOperationException($"Failed to add schedule {id}");
        }

        _logger.LogInformation("Schedule '{Name}' ({Id}) started", name, id);

        // Fire immediately if ExecuteOnStartup is set, or if any schedule is a Startup one-shot.
        var runImmediately = options.ExecuteOnStartup || AnyStartup(options.Schedules);
        if (runImmediately)
        {
            _ = Task.Run(() => entry.TriggerNow());
        }
        else
        {
            entry.ScheduleNext();
        }

        NotifyStateChange(state);
        return id;
    }

    private static bool AnyStartup(Schedule[] schedules)
    {
        for (var i = 0; i < schedules.Length; i++)
        {
            if (schedules[i].IsStartup)
                return true;
        }
        return false;
    }

    public bool Stop(ScheduleId id)
    {
        if (!_schedules.TryRemove(id, out var entry))
            return false;

        entry.UpdateStatus(ScheduleStatus.Stopped);
        NotifyStateChange(entry.State);
        entry.Dispose();

        _logger.LogInformation("Schedule '{Name}' ({Id}) stopped", entry.State.Name, id);
        return true;
    }

    public bool Pause(ScheduleId id)
    {
        if (!_schedules.TryGetValue(id, out var entry))
            return false;

        if (entry.State.Status != ScheduleStatus.Running)
            return false;

        entry.Pause();
        NotifyStateChange(entry.State);

        _logger.LogInformation("Schedule '{Name}' ({Id}) paused", entry.State.Name, id);
        return true;
    }

    public bool Resume(ScheduleId id)
    {
        if (!_schedules.TryGetValue(id, out var entry))
            return false;

        if (entry.State.Status != ScheduleStatus.Paused)
            return false;

        entry.Resume();
        NotifyStateChange(entry.State);

        _logger.LogInformation("Schedule '{Name}' ({Id}) resumed", entry.State.Name, id);
        return true;
    }

    public bool TriggerNow(ScheduleId id)
    {
        if (!_schedules.TryGetValue(id, out var entry))
            return false;

        _ = Task.Run(() => entry.TriggerNow());
        return true;
    }

    public ScheduleState? GetState(ScheduleId id)
    {
        return _schedules.TryGetValue(id, out var entry) ? entry.State : null;
    }

    public ScheduleState? FindByExtensionId(Guid extensionId)
    {
        return _schedules.Values
            .FirstOrDefault(e => e.State.ExtensionId == extensionId)
            ?.State;
    }

    public IReadOnlyCollection<ScheduleState> FindAllByExtensionId(Guid extensionId)
    {
        return _schedules.Values
            .Where(e => e.State.ExtensionId == extensionId)
            .Select(e => e.State)
            .ToList();
    }

    public IReadOnlyCollection<ScheduleState> GetAllSchedules()
    {
        return _schedules.Values.Select(e => e.State).ToList();
    }

    public IReadOnlyCollection<ScheduleState> GetActiveSchedules()
    {
        return _schedules.Values
            .Where(e => e.State.Status is ScheduleStatus.Running or ScheduleStatus.Paused)
            .Select(e => e.State)
            .ToList();
    }

    public IDisposable OnChange(Action<ScheduleState> handler)
    {
        lock (_handlersLock)
        {
            _globalHandlers.Add(handler);
        }

        return new Unsubscriber(() =>
        {
            lock (_handlersLock)
            {
                _globalHandlers.Remove(handler);
            }
        });
    }

    public IDisposable OnChange(ScheduleId id, Action<ScheduleState> handler)
    {
        var handlers = _stateHandlers.GetOrAdd(id, _ => []);
        lock (handlers)
        {
            handlers.Add(handler);
        }

        return new Unsubscriber(() =>
        {
            if (_stateHandlers.TryGetValue(id, out var list))
            {
                lock (list)
                {
                    list.Remove(handler);
                }
            }
        });
    }

    #endregion

    #region Execution

    private async Task ExecuteScheduleAsync<TData>(ScheduleEntry entry, CancellationToken cancellationToken) where TData : notnull
    {
        if (entry is not ScheduleEntry<TData> typedEntry)
            return;

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var handlers = scope.ServiceProvider.GetServices<IScheduleHandler<TData>>();

            foreach (var handler in handlers)
            {
                await handler.HandleAsync(typedEntry.Data, cancellationToken);
            }

            entry.RecordSuccess(startedAt);
            _logger.LogDebug("Schedule '{Name}' executed successfully (#{Count})",
                entry.State.Name, entry.State.ExecutionCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Schedule '{Name}' execution cancelled", entry.State.Name);
        }
        catch (Exception ex)
        {
            entry.RecordFailure(ex);
            _logger.LogError(ex, "Schedule '{Name}' execution failed", entry.State.Name);
        }
        finally
        {
            NotifyStateChange(entry.State);

            // Schedule next execution if still running
            if (entry.State.Status == ScheduleStatus.Running)
            {
                entry.ScheduleNext();
            }
        }
    }

    #endregion

    #region Helpers

    private void NotifyStateChange(ScheduleState state)
    {
        // Notify specific handlers
        if (_stateHandlers.TryGetValue(state.Id, out var handlers))
        {
            List<Action<ScheduleState>> handlersCopy;
            lock (handlers)
            {
                handlersCopy = [.. handlers];
            }

            foreach (var handler in handlersCopy)
            {
                try { handler(state); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in schedule state handler");
                }
            }
        }

        // Notify global handlers
        List<Action<ScheduleState>> globalCopy;
        lock (_handlersLock)
        {
            globalCopy = [.. _globalHandlers];
        }

        foreach (var handler in globalCopy)
        {
            try { handler(state); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in global schedule state handler");
            }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in _schedules.Values)
        {
            entry.Dispose();
        }

        _schedules.Clear();
        _stateHandlers.Clear();

        lock (_handlersLock)
        {
            _globalHandlers.Clear();
        }
    }

    #endregion

    private sealed class Unsubscriber : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Unsubscriber(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _unsubscribe();
        }
    }
}
