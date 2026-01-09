using System.Collections.Concurrent;

namespace Zonit.Messaging.Tasks;

/// <summary>
/// Przechowuje stan zadañ i zarz¹dza subskrypcjami zmian.
/// </summary>
internal sealed class TaskStateStore
{
    private readonly ConcurrentDictionary<Guid, TaskState> _tasks = new();
    private readonly ConcurrentDictionary<Guid, Action<TaskState>> _globalSubscribers = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Action<TaskState>>> _extensionSubscribers = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Action<TaskState>>> _typeSubscribers = new();
    private readonly ConcurrentDictionary<(string, Guid), ConcurrentDictionary<Guid, Action<TaskState>>> _typeExtensionSubscribers = new();

    /// <summary>
    /// Tworzy nowy stan zadania.
    /// </summary>
    public TaskState CreateTask(Guid taskId, string taskType, Guid? extensionId, int? totalSteps, object? taskData = null)
    {
        var state = new TaskState
        {
            TaskId = taskId,
            TaskType = taskType,
            ExtensionId = extensionId,
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
    /// Aktualizuje postêp zadania.
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
    /// Oznacza zadanie jako rozpoczête.
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
    /// Oznacza zadanie jako zakoñczone.
    /// </summary>
    public void CompleteTask(Guid taskId)
    {
        UpdateTask(taskId, state =>
        {
            state.Status = TaskStatus.Completed;
            state.CompletedAt = DateTimeOffset.UtcNow;
            state.Progress = 100;
        });
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
    }

    /// <summary>
    /// Usuwa zakoñczone zadania starsze ni¿ podany czas.
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
        return _tasks.TryGetValue(taskId, out var state) ? state : null;
    }

    /// <summary>
    /// Pobiera aktywne zadania.
    /// </summary>
    public IReadOnlyCollection<TaskState> GetActiveTasks(Guid? extensionId = null)
    {
        var activeStatuses = new[] { TaskStatus.Pending, TaskStatus.Processing };
        
        return _tasks.Values
            .Where(s => activeStatuses.Contains(s.Status))
            .Where(s => !extensionId.HasValue || s.ExtensionId == extensionId)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Subskrybuje zmiany wszystkich zadañ.
    /// </summary>
    public IDisposable Subscribe(Action<TaskState> handler)
    {
        var subscriptionId = Guid.NewGuid();
        _globalSubscribers[subscriptionId] = handler;

        return new Subscription(() => _globalSubscribers.TryRemove(subscriptionId, out _));
    }

    /// <summary>
    /// Subskrybuje zmiany zadañ dla konkretnego ExtensionId.
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
    /// Subskrybuje zmiany zadañ okreœlonego typu.
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
    /// Subskrybuje zmiany zadañ okreœlonego typu dla konkretnego ExtensionId.
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

    private void NotifySubscribers(TaskState state)
    {
        // Global subscribers
        foreach (var handler in _globalSubscribers.Values)
        {
            try
            {
                handler(state);
            }
            catch
            {
                // Ignore subscriber exceptions
            }
        }

        // Extension-specific subscribers
        if (state.ExtensionId.HasValue && 
            _extensionSubscribers.TryGetValue(state.ExtensionId.Value, out var extensionSubs))
        {
            foreach (var handler in extensionSubs.Values)
            {
                try
                {
                    handler(state);
                }
                catch
                {
                    // Ignore subscriber exceptions
                }
            }
        }

        // Type-specific subscribers
        if (_typeSubscribers.TryGetValue(state.TaskType, out var typeSubs))
        {
            foreach (var handler in typeSubs.Values)
            {
                try
                {
                    handler(state);
                }
                catch
                {
                    // Ignore subscriber exceptions
                }
            }
        }

        // Type + Extension specific subscribers
        if (state.ExtensionId.HasValue &&
            _typeExtensionSubscribers.TryGetValue((state.TaskType, state.ExtensionId.Value), out var typeExtSubs))
        {
            foreach (var handler in typeExtSubs.Values)
            {
                try
                {
                    handler(state);
                }
                catch
                {
                    // Ignore subscriber exceptions
                }
            }
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
}
