using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Messaging.Events;

namespace Zonit.Messaging.Tests;

public sealed record TxEvent(int Seq);

public sealed class TxHandler(Recorder r) : IEventHandler<TxEvent>
{
    public int WorkerCount => 1;
    public Task HandleAsync(TxEvent data, CancellationToken ct)
    {
        r.Log.Enqueue($"tx:{data.Seq}");
        return Task.CompletedTask;
    }
}

public class EventTransactionTests
{
    [Fact]
    public async Task Transaction_buffers_until_completion_and_dispatches_in_order()
    {
        await using var h = await TestHost.StartAsync(s => s.AddEventHandlers());
        var events = h.Get<IEventProvider>();

        await using (var tx = events.CreateTransaction())
        {
            events.Publish(new TxEvent(1));
            events.Publish(new TxEvent(2));
            events.Publish(new TxEvent(3));

            // Buffered, not dispatched yet.
            h.Recorder.Log.Should().BeEmpty();
            tx.Count.Should().Be(3);

            await tx.WaitForCompletionAsync();

            h.Recorder.Log.Where(x => x.StartsWith("tx:"))
                .Should().Equal("tx:1", "tx:2", "tx:3");
        }
    }

    [Fact]
    public async Task Concurrent_publish_into_ambient_transaction_is_thread_safe()
    {
        await using var h = await TestHost.StartAsync(s => s.AddEventHandlers());
        var events = h.Get<IEventProvider>();
        const int n = 200;

        await using (var tx = events.CreateTransaction())
        {
            // Many concurrent publishes attach to the same ambient transaction. Without the
            // lock around the events list this loses/corrupts entries.
            await Task.WhenAll(Enumerable.Range(0, n)
                .Select(i => Task.Run(() => events.Publish(new TxEvent(i)))));

            tx.Count.Should().Be(n);
            await tx.WaitForCompletionAsync();
        }

        h.Recorder.Log.Count(x => x.StartsWith("tx:")).Should().Be(n);
    }

    [Fact]
    public async Task WaitForCompletion_without_explicit_commit_still_dispatches()
    {
        await using var h = await TestHost.StartAsync(s => s.AddEventHandlers());
        var events = h.Get<IEventProvider>();

        await using var tx = events.CreateTransaction();
        events.Publish(new TxEvent(42));

        // No explicit CommitAsync: WaitForCompletionAsync must commit-then-wait (not a no-op).
        await tx.WaitForCompletionAsync();

        h.Recorder.Log.Should().Contain("tx:42");
    }

    [Fact]
    public async Task Enqueue_after_commit_throws()
    {
        await using var h = await TestHost.StartAsync(s => s.AddEventHandlers());
        var events = h.Get<IEventProvider>();

        var tx = events.CreateTransaction();
        events.Publish(new TxEvent(1));
        await tx.CommitAsync();

        var act = () => tx.Enqueue(new TxEvent(2));
        act.Should().Throw<InvalidOperationException>();

        await tx.DisposeAsync();
    }
}
