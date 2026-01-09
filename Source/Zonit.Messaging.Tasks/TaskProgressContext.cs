using System.Diagnostics;

namespace Zonit.Messaging.Tasks;

/// <summary>
/// Implementacja kontekstu postêpu z time-based smooth progress.
/// Automatycznie wysy³a aktualizacje gdy % siê zmieni (max 100 razy).
/// </summary>
internal sealed class TaskProgressContext : ITaskProgressContext, IDisposable
{
    private readonly TaskProgressStep[] _steps;
    private readonly Action<int, int?, string?> _onProgressChanged;
    private readonly double[] _stepEndPercentages;
    private readonly Stopwatch _stepStopwatch = new();
    private readonly Timer? _progressTimer;
    private readonly object _lock = new();
    
    private int _currentStepIndex = -1;
    private int _lastReportedProgress = -1;
    private string? _lastMessage;
    private bool _disposed;

    public TaskProgressContext(
        TaskProgressStep[]? steps,
        Action<int, int?, string?> onProgressChanged)
    {
        _steps = steps ?? [];
        _onProgressChanged = onProgressChanged;
        _stepEndPercentages = CalculateStepEndPercentages(_steps);

        // Uruchom timer tylko jeœli mamy kroki do œledzenia
        if (_steps.Length > 0)
        {
            // Timer co 200ms sprawdza czy % siê zmieni³
            _progressTimer = new Timer(
                _ => UpdateTimeBasedProgress(),
                null,
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(200));
        }
    }

    public int CurrentStepIndex => _currentStepIndex;
    public int TotalSteps => _steps.Length;
    public int CurrentProgress => CalculateCurrentProgress();

    public Task NextAsync(string? message = null)
    {
        return GoToAsync(_currentStepIndex + 1, message);
    }

    public Task GoToAsync(int stepIndex, string? message = null)
    {
        if (_steps.Length == 0)
        {
            return Task.CompletedTask;
        }

        if (stepIndex < 0 || stepIndex >= _steps.Length)
        {
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            _currentStepIndex = stepIndex;
            _stepStopwatch.Restart();

            var step = _steps[stepIndex];
            var displayMessage = message ?? step.Message;
            _lastMessage = displayMessage;
            var progress = CalculateCurrentProgress();

            NotifyIfChanged(progress, stepIndex + 1, displayMessage);
        }

        return Task.CompletedTask;
    }

    public Task SetMessageAsync(string message)
    {
        lock (_lock)
        {
            _lastMessage = message;
            var progress = CalculateCurrentProgress();
            var stepNumber = _currentStepIndex >= 0 ? _currentStepIndex + 1 : (int?)null;
            
            NotifyIfChanged(progress, stepNumber, message);
        }
        
        return Task.CompletedTask;
    }

    public Task SetProgressAsync(int percentage, string? message = null)
    {
        lock (_lock)
        {
            var clampedPercentage = Math.Clamp(percentage, 0, 100);
            var stepNumber = _currentStepIndex >= 0 ? _currentStepIndex + 1 : (int?)null;
            
            if (message is not null)
            {
                _lastMessage = message;
            }
            
            NotifyIfChanged(clampedPercentage, stepNumber, message);
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Wywo³ywane przez timer - aktualizuje postêp na podstawie up³ywu czasu.
    /// </summary>
    private void UpdateTimeBasedProgress()
    {
        // Double-check pattern dla thread-safety
        if (_disposed) return;

        // U¿ywamy Monitor.TryEnter ¿eby unikn¹æ blokowania timera
        if (!Monitor.TryEnter(_lock))
            return;

        try
        {
            if (_disposed) return;
            
            if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length)
            {
                return;
            }

            var progress = CalculateCurrentProgress();
            var stepNumber = _currentStepIndex + 1;

            // Wysy³aj tylko gdy % siê zmieni (bez zmiany message)
            NotifyIfChanged(progress, stepNumber, message: null);
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    private int CalculateCurrentProgress()
    {
        if (_steps.Length == 0 || _currentStepIndex < 0)
        {
            return 0;
        }

        var stepStartPercentage = _currentStepIndex == 0 ? 0.0 : _stepEndPercentages[_currentStepIndex - 1];
        var stepEndPercentage = _stepEndPercentages[_currentStepIndex];
        var stepRange = stepEndPercentage - stepStartPercentage;

        var step = _steps[_currentStepIndex];
        var elapsed = _stepStopwatch.Elapsed;
        var estimatedDuration = step.EstimatedDuration;

        if (estimatedDuration <= TimeSpan.Zero)
        {
            return (int)stepEndPercentage;
        }

        // Oblicz postêp w ramach kroku (max 99% ¿eby nie przeskoczyæ do nastêpnego)
        var stepProgress = Math.Min(elapsed.TotalMilliseconds / estimatedDuration.TotalMilliseconds, 0.99);
        var totalProgress = stepStartPercentage + (stepRange * stepProgress);

        return (int)Math.Clamp(totalProgress, 0, 100);
    }

    private void NotifyIfChanged(int progress, int? stepNumber, string? message)
    {
        // Wysy³aj tylko gdy % siê zmieni lub gdy jest nowa wiadomoœæ
        if (progress != _lastReportedProgress || message is not null)
        {
            _lastReportedProgress = progress;
            _onProgressChanged(progress, stepNumber, message ?? _lastMessage);
        }
    }

    private static double[] CalculateStepEndPercentages(TaskProgressStep[] steps)
    {
        if (steps.Length == 0)
        {
            return [];
        }

        var totalDuration = TimeSpan.Zero;
        foreach (var step in steps)
        {
            totalDuration += step.EstimatedDuration;
        }

        if (totalDuration <= TimeSpan.Zero)
        {
            var equalPercentage = 100.0 / steps.Length;
            var result = new double[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                result[i] = equalPercentage * (i + 1);
            }
            return result;
        }

        var endPercentages = new double[steps.Length];
        var cumulativeDuration = TimeSpan.Zero;

        for (int i = 0; i < steps.Length; i++)
        {
            cumulativeDuration += steps[i].EstimatedDuration;
            endPercentages[i] = (cumulativeDuration.TotalMilliseconds / totalDuration.TotalMilliseconds) * 100.0;
        }

        return endPercentages;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        
        // Dispose timera poza lockiem ¿eby unikn¹æ deadlocka
        _progressTimer?.Dispose();
    }
}
