using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Messaging.Events;

namespace Zonit.Messaging.Tests;

public sealed record OrderPlaced(int Id);

// Two handlers for the same event prove fan-out.
public sealed class OrderLogger(Recorder r) : IEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced data, CancellationToken ct)
    {
        r.Log.Enqueue($"logger:{data.Id}");
        r.Signal($"logger:{data.Id}").TrySetResult();
        return Task.CompletedTask;
    }
}

public sealed class OrderAuditor(Recorder r) : IEventHandler<OrderPlaced>
{
    public int WorkerCount => 1;                        // exercises a per-handler DIM override
    public Task HandleAsync(OrderPlaced data, CancellationToken ct)
    {
        r.Log.Enqueue($"auditor:{data.Id}");
        r.Signal($"auditor:{data.Id}").TrySetResult();
        return Task.CompletedTask;
    }
}

public sealed record Flaky(int Id);

// First invocation throws; with the default ContinueOnError the channel keeps draining.
public sealed class FlakyHandler(Recorder r) : IEventHandler<Flaky>
{
    public Task HandleAsync(Flaky data, CancellationToken ct)
    {
        r.Log.Enqueue($"flaky:{data.Id}");
        r.Signal($"flaky:{data.Id}").TrySetResult();
        if (data.Id == 1) throw new InvalidOperationException("boom");
        return Task.CompletedTask;
    }
}

public class EventTests
{
    [Fact]
    public async Task Publish_fans_out_to_all_handlers()
    {
        await using var h = await TestHost.StartAsync(s => s.AddEventHandlers());

        h.Get<IEventProvider>().Publish(new OrderPlaced(7));

        await h.Recorder.WaitAsync("logger:7");
        await h.Recorder.WaitAsync("auditor:7");
        h.Recorder.Log.Should().Contain(["logger:7", "auditor:7"]);
    }

    [Fact]
    public async Task Handler_exception_does_not_stop_the_channel()
    {
        await using var h = await TestHost.StartAsync(s => s.AddEventHandlers());
        var events = h.Get<IEventProvider>();

        events.Publish(new Flaky(1));   // throws inside the handler
        events.Publish(new Flaky(2));   // must still be processed (ContinueOnError = true)

        await h.Recorder.WaitAsync("flaky:2");
        h.Recorder.Log.Should().Contain("flaky:2");
    }

    [Fact]
    public async Task Manual_AddEvent_registers_a_handler()
    {
        await using var h = await TestHost.StartAsync(s => s.AddEvent<OrderLogger, OrderPlaced>());

        h.Get<IEventProvider>().Publish(new OrderPlaced(99));

        await h.Recorder.WaitAsync("logger:99");
        h.Recorder.Log.Should().Contain("logger:99");
    }
}
