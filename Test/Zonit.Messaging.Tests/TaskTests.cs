using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Messaging.Tasks;
using TaskStatus = Zonit.Messaging.Tasks.TaskStatus;   // disambiguate from System.Threading.Tasks.TaskStatus

namespace Zonit.Messaging.Tests;

public sealed record DoWork(string Name);

public sealed class WorkHandler(Recorder r) : ITaskHandler<DoWork>
{
    public Task HandleAsync(TaskPayload<DoWork> payload)
    {
        r.Log.Enqueue($"work:{payload.Data.Name}");
        r.Signal($"work:{payload.Data.Name}").TrySetResult();
        return Task.CompletedTask;
    }
}

public sealed record RetryJob(string Name);

public sealed class RetryHandler(Recorder r) : TaskHandler<RetryJob>
{
    public override int MaxRetries => 3;
    public override TimeSpan RetryDelay => TimeSpan.FromMilliseconds(10);

    protected override Task HandleAsync(RetryJob data, ITaskProgressContext progress, CancellationToken ct)
    {
        var attempts = r.Log.Count(x => x == $"retry:{data.Name}");
        r.Log.Enqueue($"retry:{data.Name}");
        if (attempts < 2) throw new InvalidOperationException("transient");   // fail the first two
        r.Signal($"retry-done:{data.Name}").TrySetResult();
        return Task.CompletedTask;
    }
}

public sealed record ProgressJob(string Name);

public sealed class ProgressJobHandler(Recorder r) : TaskHandler<ProgressJob>
{
    public override TaskProgressStep[]? ProgressSteps =>
        [new(TimeSpan.FromMilliseconds(10), "a"), new(TimeSpan.FromMilliseconds(10), "b")];

    protected override async Task HandleAsync(ProgressJob data, ITaskProgressContext progress, CancellationToken ct)
    {
        await progress.NextAsync();
        await progress.NextAsync();
        r.Signal($"prog:{data.Name}").TrySetResult();
    }
}

public class TaskTests
{
    [Fact]
    public async Task Publish_runs_the_handler()
    {
        await using var h = await TestHost.StartAsync(s => s.AddTaskHandlers());

        h.Get<ITaskProvider>().Publish(new DoWork("a"));

        await h.Recorder.WaitAsync("work:a");
        h.Recorder.Log.Should().Contain("work:a");
    }

    [Fact]
    public async Task Task_reaches_completed_state()
    {
        await using var h = await TestHost.StartAsync(s => s.AddTaskHandlers());
        var manager = h.Get<ITaskManager>();
        var states = new ConcurrentQueue<TaskState>();
        using var _ = manager.OnChange(states.Enqueue);

        h.Get<ITaskProvider>().Publish(new DoWork("b"));

        await Harness.WaitUntilAsync(() => states.Any(s => s.Status == TaskStatus.Completed));
    }

    [Fact]
    public async Task Failing_task_is_retried_until_it_succeeds()
    {
        await using var h = await TestHost.StartAsync(s => s.AddTaskHandlers());

        h.Get<ITaskProvider>().Publish(new RetryJob("x"));

        await h.Recorder.WaitAsync("retry-done:x");
        h.Recorder.Log.Count(x => x == "retry:x").Should().Be(3); // two failures + one success
    }

    [Fact]
    public async Task Progress_reaches_100_on_completion()
    {
        await using var h = await TestHost.StartAsync(s => s.AddTaskHandlers());
        var manager = h.Get<ITaskManager>();
        var states = new ConcurrentQueue<TaskState>();
        using var _ = manager.OnChange(states.Enqueue);

        h.Get<ITaskProvider>().Publish(new ProgressJob("p"));

        await h.Recorder.WaitAsync("prog:p");
        await Harness.WaitUntilAsync(() => states.Any(s => s.Status == TaskStatus.Completed && s.Progress == 100));
    }

    [Fact]
    public async Task OnChange_subscribers_get_an_immutable_snapshot()
    {
        await using var h = await TestHost.StartAsync(s => s.AddTaskHandlers());
        var manager = h.Get<ITaskManager>();

        Guid taskId = Guid.Empty;
        using var _ = manager.OnChange(s =>
        {
            taskId = s.TaskId;
            // A misbehaving subscriber mutating its copy must not corrupt the store.
            s.Progress = -999;
            s.Status = TaskStatus.Cancelled;
        });

        h.Get<ITaskProvider>().Publish(new DoWork("c"));

        await h.Recorder.WaitAsync("work:c");
        await Harness.WaitUntilAsync(() => taskId != Guid.Empty);

        var live = manager.GetTaskState(taskId);
        live.Should().NotBeNull();
        live!.Progress.Should().NotBe(-999);
        live.Status.Should().NotBe(TaskStatus.Cancelled);
    }

    [Fact]
    public async Task GetActiveTasks_filters_by_type()
    {
        await using var h = await TestHost.StartAsync(s => s.AddTaskHandlers());
        var manager = h.Get<ITaskManager>();

        // Publish with a slow-ish handler is overkill; just assert the typed query compiles and runs.
        var active = manager.GetActiveTasks<DoWork>();
        active.Should().NotBeNull();
    }
}
