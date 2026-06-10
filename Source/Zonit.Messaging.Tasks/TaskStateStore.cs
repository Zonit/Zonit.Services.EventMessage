using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Zonit.Messaging.Tasks;

/// <summary>
/// Przechowuje stan zada� i zarz�dza subskrypcjami zmian.
/// </summary>
internal sealed class TaskStateStore
{
    /// <summary>
    /// Domyślny górny limit przechowywanych stanów zadań. Po przekroczeniu usuwane są
    /// najstarsze zakończone zadania (FIFO), nawet jeśli nie minął jeszcze czas retencji.
    /// Chroni przed nieograniczonym wzrostem pamięci przy zalewie publikacji.
    /// </summary>
    private const int DefaultMaxRetainedTasks = 10_000;

    private readonly ConcurrentDictionary<Guid, TaskState> _tasks = new();
    private readonly ConcurrentDictionary<Guid, Action<TaskState>> _globalSubscribers = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Action<TaskState>>> _extensionSubscribers = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Action<TaskState>>> _typeSubscribers = new();
    private readonly ConcurrentDictionary<(string, Guid), ConcurrentDictionary<Guid, Action<TaskState>>> _typeExtensionSubscribers = new();
    // FIFO kolejność zadań w stanie terminalnym (Completed/Failed/Cancelled) — używana do
    // eksmisji najstarszych po przekroczeniu limitu retencji. Aktywne zadania nigdy tu nie trafiają.
    private readonly ConcurrentQueue<Guid> _terminalOrder = new();
    private readonly int _maxRetainedTasks;
    private readonly ILogger _logger;

    public TaskStateStore(ILogger logger, int maxRetainedTasks = DefaultMaxRetainedTasks)
    {
        _logger = logger;
        _maxRetainedTasks = maxRetainedTasks > 0 ? maxRetainedTasks : DefaultMaxRetainedTasks;
    }

    /// <summary>
    /// Tworzy nowy stan zadania.
    /// </summary>
    public TaskState CreateTask(Guid taskId, string taskType, Guid? extensionId, int? totalSteps, string? title, string? description, object? taskData = null)
    {
        var state = new TaskState
        {
            TaskId = taskId,
            TaskType = taskType,
            ExtensionId = extensionId,
            Title = title,
            Description = description,
            Status = TaskStatus.Pending,
            TotalSteps = totalSteps,
            CurrentStep = totalSteps.HasValue ? 0 : null,
            Progress = totalSteps.HasValue ? 0 : null,
            TaskData = taskData
        };

        _tasks[taskId] = state;
        NotifySubscribers(state);

        return state;
    }

    /// <summary>
    /// Aktualizuje stan zadania.
    /// </summary>
    public void UpdateTask(Guid taskId, Action<TaskState> update)
    {
        if (_tasks.TryGetValue(taskId, out var state))
        {
            update(state);
            NotifySubscribers(state);
        }
    }

    /// <summary>
    /// Aktualizuje post�p zadania.
    /// </summary>
    public void UpdateProgress(Guid taskId, int progress, int? currentStep, string? message)
    {
        if (_tasks.TryGetValue(taskId, out var state))
        {
            var changed = state.Progress != progress ||
                          state.CurrentStep != currentStep ||
                          (message is not null && state.Message != message);

            if (changed)
            {
                state.Progress = progress;
                if (currentStep.HasValue)
                {
                    state.CurrentStep = currentStep;
                }
                if (message is not null)
                {
                    state.Message = message;
                }
                NotifySubscribers(state);
            }
        }
    }

    /// <summary>
    /// Oznacza zadanie jako rozpocz�te.
    /// </summary>
    public void StartTask(Guid taskId)
    {
        UpdateTask(taskId, state =>
        {
            state.Status = TaskStatus.Processing;
            state.StartedAt = DateTimeOffset.UtcNow;
        });
    }

    /// <summary>
    /// Oznacza zadanie jako zako�czone.
    /// </summary>
    public void CompleteTask(Guid taskId)
    {
        UpdateTask(taskId, state =>
        {
            state.Status = TaskStatus.Completed;
            state.CompletedAt = DateTimeOffset.UtcNow;
            state.Progress = 100;
        });
        TrackTerminal(taskId);
    }

    /// <summary>
    /// Oznacza zadanie jako nieudane.
    /// </summary>
    public void FailTask(Guid taskId)
    {
        UpdateTask(taskId, state =>
        {
            state.Status = TaskStatus.Failed;
            state.CompletedAt = DateTimeOffset.UtcNow;
        });
        TrackTerminal(taskId);
    }

    /// <summary>
    /// Oznacza zadanie jako anulowane.
    /// </summary>
    public void CancelTask(Guid taskId)
    {
        UpdateTask(taskId, state =>
        {
            state.Status = TaskStatus.Cancelled;
            state.CompletedAt = DateTimeOffset.UtcNow;
        });
        TrackTerminal(taskId);
    }

    /// <summary>
    /// Rejestruje zadanie w stanie terminalnym i eksmituje najstarsze, gdy liczba przechowywanych
    /// zakończonych zadań przekroczy <see cref="_maxRetainedTasks"/>. Daje twardy limit pamięci
    /// niezależny od czasowego <see cref="CleanupOldTasks"/> (który usuwa dopiero po 30 min).
    /// </summary>
    private void TrackTerminal(Guid taskId)
    {
        _terminalOrder.Enqueue(taskId);

        while (_terminalOrder.Count > _maxRetainedTasks && _terminalOrder.TryDequeue(out var oldId))
        {
            _tasks.TryRemove(oldId, out _);
        }
    }

    /// <summary>
    /// Usuwa zako�czone zadania starsze ni� podany czas.
    /// </summary>
    public int CleanupOldTasks(TimeSpan maxAge)
    {
        var threshold = DateTimeOffset.UtcNow - maxAge;
        var toRemove = new List<Guid>();

        foreach (var kvp in _tasks)
        {
            var state = kvp.Value;
            if (state.Status is TaskStatus.Completed or TaskStatus.Failed or TaskStatus.Cancelled &&
                state.CompletedAt.HasValue &&
                state.CompletedAt.Value < threshold)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var taskId in toRemove)
        {
            _tasks.TryRemove(taskId, out _);
        }

        return toRemove.Count;
    }

    /// <summary>
    /// Pobiera stan zadania.
    /// </summary>
    public TaskState? GetTaskState(Guid taskId)
    {
        return _tasks.TryGetValue(taskId, out var state) ? state.Snapshot() : null;
    }

    /// <summary>
    /// Pobiera aktywne zadania.
    /// </summary>
    private static bool IsActive(TaskState s) => s.Status is TaskStatus.Pending or TaskStatus.Processing;

    public IReadOnlyCollection<TaskState> GetActiveTasks(Guid? extensionId = null)
    {
        return _tasks.Values
            .Where(s => IsActive(s) && (!extensionId.HasValue || s.ExtensionId == extensionId))
            .Select(s => s.Snapshot())
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Pobiera aktywne zadania określonego typu.
    /// </summary>
    public IReadOnlyCollection<TaskState<TTask>> GetActiveTasks<TTask>(Guid? extensionId = null) where TTask : notnull
    {
        var taskType = typeof(TTask).FullName ?? typeof(TTask).Name;

        return _tasks.Values
            .Where(s => IsActive(s) && s.TaskType == taskType && (!extensionId.HasValue || s.ExtensionId == extensionId))
            .Select(s => TaskState<TTask>.FromBase(s))
            .Where(s => s is not null)
            .Cast<TaskState<TTask>>()
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Pobiera aktywne zadania dla wielu określonych typów.
    /// </summary>
    public IReadOnlyCollection<TaskState> GetActiveTasks(Type[] taskTypes, Guid? extensionId = null)
    {
        var typeNames = taskTypes.Select(t => t.FullName ?? t.Name).ToHashSet();

        return _tasks.Values
            .Where(s => IsActive(s) && typeNames.Contains(s.TaskType) && (!extensionId.HasValue || s.ExtensionId == extensionId))
            .Select(s => s.Snapshot())
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Subskrybuje zmiany wszystkich zadań.
    /// </summary>
    public IDisposable Subscribe(Action<TaskState> handler)
    {
        var subscriptionId = Guid.NewGuid();
        _globalSubscribers[subscriptionId] = handler;

        return new Subscription(() => _globalSubscribers.TryRemove(subscriptionId, out _));
    }

    /// <summary>
    /// Subskrybuje zmiany zada� dla konkretnego ExtensionId.
    /// </summary>
    public IDisposable Subscribe(Guid extensionId, Action<TaskState> handler)
    {
        var subscriptionId = Guid.NewGuid();

        var extensionSubs = _extensionSubscribers.GetOrAdd(extensionId, _ => new ConcurrentDictionary<Guid, Action<TaskState>>());
        extensionSubs[subscriptionId] = handler;

        return new Subscription(() =>
        {
            extensionSubs.TryRemove(subscriptionId, out _);
            if (extensionSubs.IsEmpty)
            {
                _extensionSubscribers.TryRemove(extensionId, out _);
            }
        });
    }

    /// <summary>
    /// Subskrybuje zmiany zada� okre�lonego typu.
    /// </summary>
    public IDisposable Subscribe<TTask>(Action<TaskState<TTask>> handler) where TTask : notnull
    {
        var subscriptionId = Guid.NewGuid();
        var taskType = typeof(TTask).FullName ?? typeof(TTask).Name;

        var typeSubs = _typeSubscribers.GetOrAdd(taskType, _ => new ConcurrentDictionary<Guid, Action<TaskState>>());
        typeSubs[subscriptionId] = state =>
        {
            var typedState = TaskState<TTask>.FromBase(state);
            if (typedState is not null)
            {
                handler(typedState);
            }
        };

        return new Subscription(() =>
        {
            typeSubs.TryRemove(subscriptionId, out _);
            if (typeSubs.IsEmpty)
            {
                _typeSubscribers.TryRemove(taskType, out _);
            }
        });
    }

    /// <summary>
    /// Subskrybuje zmiany zadań określonego typu dla konkretnego ExtensionId.
    /// </summary>
    public IDisposable Subscribe<TTask>(Guid extensionId, Action<TaskState<TTask>> handler) where TTask : notnull
    {
        var subscriptionId = Guid.NewGuid();
        var taskType = typeof(TTask).FullName ?? typeof(TTask).Name;
        var key = (taskType, extensionId);

        var typeSubs = _typeExtensionSubscribers.GetOrAdd(key, _ => new ConcurrentDictionary<Guid, Action<TaskState>>());
        typeSubs[subscriptionId] = state =>
        {
            var typedState = TaskState<TTask>.FromBase(state);
            if (typedState is not null)
            {
                handler(typedState);
            }
        };

        return new Subscription(() =>
        {
            typeSubs.TryRemove(subscriptionId, out _);
            if (typeSubs.IsEmpty)
            {
                _typeExtensionSubscribers.TryRemove(key, out _);
            }
        });
    }

    /// <summary>
    /// Subskrybuje zmiany zadań dla wielu określonych typów.
    /// </summary>
    public IDisposable Subscribe(Type[] taskTypes, Action<TaskState> handler)
    {
        var typeNames = taskTypes.Select(t => t.FullName ?? t.Name).ToHashSet();
        var subscriptions = new List<IDisposable>();

        foreach (var typeName in typeNames)
        {
            var subscriptionId = Guid.NewGuid();
            var typeSubs = _typeSubscribers.GetOrAdd(typeName, _ => new ConcurrentDictionary<Guid, Action<TaskState>>());
            typeSubs[subscriptionId] = handler;

            subscriptions.Add(new Subscription(() =>
            {
                typeSubs.TryRemove(subscriptionId, out _);
                if (typeSubs.IsEmpty)
                {
                    _typeSubscribers.TryRemove(typeName, out _);
                }
            }));
        }

        return new CompositeSubscription(subscriptions);
    }

    private void NotifySubscribers(TaskState state)
    {
        // Resolve the matching subscriber buckets up front (against the live state — TaskType and
        // ExtensionId are immutable, so the lookups are identical to the snapshot's).
        var hasGlobal = !_globalSubscribers.IsEmpty;

        ConcurrentDictionary<Guid, Action<TaskState>>? extensionSubs = null;
        ConcurrentDictionary<Guid, Action<TaskState>>? typeExtSubs = null;
        if (state.ExtensionId.HasValue)
        {
            _extensionSubscribers.TryGetValue(state.ExtensionId.Value, out extensionSubs);
            _typeExtensionSubscribers.TryGetValue((state.TaskType, state.ExtensionId.Value), out typeExtSubs);
        }

        _typeSubscribers.TryGetValue(state.TaskType, out var typeSubs);

        // Fast path: nothing is listening for this task — skip the snapshot allocation entirely.
        // The common case (no UI bound) previously allocated a throwaway snapshot per state change.
        if (!hasGlobal
            && (extensionSubs is null || extensionSubs.IsEmpty)
            && (typeSubs is null || typeSubs.IsEmpty)
            && (typeExtSubs is null || typeExtSubs.IsEmpty))
        {
            return;
        }

        // Hand subscribers an immutable snapshot, not the live object that workers keep mutating.
        var snapshot = state.Snapshot();

        if (hasGlobal)
        {
            foreach (var handler in _globalSubscribers.Values)
                Invoke(handler, snapshot);
        }

        if (extensionSubs is not null)
        {
            foreach (var handler in extensionSubs.Values)
                Invoke(handler, snapshot);
        }

        if (typeSubs is not null)
        {
            foreach (var handler in typeSubs.Values)
                Invoke(handler, snapshot);
        }

        if (typeExtSubs is not null)
        {
            foreach (var handler in typeExtSubs.Values)
                Invoke(handler, snapshot);
        }
    }

    private void Invoke(Action<TaskState> handler, TaskState snapshot)
    {
        try
        {
            handler(snapshot);
        }
        catch (Exception ex)
        {
            // A faulty subscriber must not break task processing, but the failure is logged
            // (previously swallowed silently).
            _logger.LogError(ex, "Task state subscriber threw for task '{TaskType}' ({TaskId})",
                snapshot.TaskType, snapshot.TaskId);
        }
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            onDispose();
        }
    }

    private sealed class CompositeSubscription(IEnumerable<IDisposable> subscriptions) : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = subscriptions.ToList();
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }
    }
}
