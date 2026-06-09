using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Extensions;            // Schedule value object
using Zonit.Messaging.Schedules;

namespace Zonit.Messaging.Tests;

// No-data handler, started via AddSchedule<T>.
public sealed class NowHandler(Recorder r) : IScheduleHandler
{
    public Task HandleAsync(CancellationToken cancellationToken)
    {
        r.Log.Enqueue("now");
        r.Signal("now").TrySetResult();
        return Task.CompletedTask;
    }
}

public sealed record OverlapData(string Name);

// Tracks concurrency; if the schedule ever overlaps itself, MaxConcurrency goes above 1.
public sealed class OverlapHandler(Recorder r) : IScheduleHandler<OverlapData>
{
    public async Task HandleAsync(OverlapData data, CancellationToken cancellationToken)
    {
        using (r.Enter())
            await Task.Delay(80, cancellationToken);
        r.Log.Enqueue("overlap-done");
    }
}

public sealed record FailData(string Name);

public sealed class AlwaysFailHandler(Recorder r) : IScheduleHandler<FailData>
{
    public Task HandleAsync(FailData data, CancellationToken cancellationToken)
    {
        r.Log.Enqueue("fail");
        throw new InvalidOperationException("always fails");
    }
}

public class ScheduleTests
{
    [Fact]
    public async Task AddSchedule_runs_a_now_handler_at_startup()
    {
        await using var h = await TestHost.StartAsync(s =>
            s.AddSchedule<NowHandler>(o => o.Schedules = [Schedule.Now()]));

        await h.Recorder.WaitAsync("now");
        h.Recorder.Log.Should().Contain("now");
    }

    [Fact]
    public async Task Provider_Start_runs_a_data_handler()
    {
        await using var h = await TestHost.StartAsync(s => s.AddScheduleHandlers());

        var id = h.Get<IScheduleProvider>().Start(new OverlapData("o"), Schedule.Now());

        await Harness.WaitUntilAsync(() => h.Recorder.Log.Contains("overlap-done"));
        h.Get<IScheduleProvider>().GetState(id).Should().NotBeNull();
    }

    [Fact]
    public async Task Schedule_never_overlaps_itself()
    {
        await using var h = await TestHost.StartAsync(s => s.AddScheduleHandlers());
        var provider = h.Get<IScheduleProvider>();

        var id = provider.Start(new OverlapData("o"), Schedule.Now());
        // Pile concurrent triggers on top of the in-flight run.
        provider.TriggerNow(id);
        provider.TriggerNow(id);
        provider.TriggerNow(id);

        await Harness.WaitUntilAsync(() => h.Recorder.Log.Count(x => x == "overlap-done") >= 1);
        await Task.Delay(500); // give any (incorrectly) overlapping runs time to appear

        h.Recorder.MaxConcurrency.Should().Be(1);
    }

    [Fact]
    public async Task StopOnMaxRetries_moves_to_failed_and_stops()
    {
        await using var h = await TestHost.StartAsync(s => s.AddScheduleHandlers());
        var provider = h.Get<IScheduleProvider>();

        var id = provider.Start(new FailData("f"), o =>
        {
            o.Schedules = [Schedule.EverySeconds(1)];
            o.ExecuteOnStartup = true;     // fire immediately
            o.MaxRetries = 1;
            o.StopOnMaxRetries = true;
            o.RetryDelay = TimeSpan.FromMilliseconds(10);
        });

        await Harness.WaitUntilAsync(() => provider.GetState(id)?.Status == ScheduleStatus.Failed);
        provider.GetState(id)!.ConsecutiveFailures.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Stop_removes_the_schedule()
    {
        await using var h = await TestHost.StartAsync(s =>
            s.AddSchedule<NowHandler>(o => o.Schedules = [Schedule.EveryHours(1)]));
        var provider = h.Get<IScheduleProvider>();

        var id = provider.GetAllSchedules().Single().Id;
        provider.Stop(id).Should().BeTrue();
        provider.GetState(id).Should().BeNull();          // stopped schedules are removed
        provider.Stop(id).Should().BeFalse();             // already gone
    }
}
