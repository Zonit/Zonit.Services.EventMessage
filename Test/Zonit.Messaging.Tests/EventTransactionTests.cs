using System.Collections.Concurrent;
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

public sealed record TxAsyncEvent(int Seq);

// Handler that actually awaits: the await captures the ambient SynchronizationContext, which is
// what triggers the synchronous-Dispose deadlock regression.
public sealed class TxAsyncHandler(Recorder r) : IEventHandler<TxAsyncEvent>
{
    public int WorkerCount => 1;
    public async Task HandleAsync(TxAsyncEvent data, CancellationToken ct)
    {
        await Task.Delay(50, ct);
        r.Log.Enqueue($"async:{data.Seq}");
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

    [Fact]
    public async Task Sync_dispose_does_not_deadlock_under_synchronization_context()
    {
        // Regression: synchronous 'using (tx)' Dispose auto-commits and blocks the calling thread.
        // If that thread carries a SynchronizationContext (Blazor Server circuit, WPF/WinForms UI
        // thread), a handler that awaits would post its continuation back to the now-blocked thread
        // and deadlock. The fix starts the whole pipeline on the thread pool (no captured context).
        await using var h = await TestHost.StartAsync(s => s.AddEventHandlers());
        var events = h.Get<IEventProvider>();

        using var ctx = new SingleThreadSyncContext();

        // Execute the synchronous using-block ON the single-threaded context.
        var work = ctx.Run(() =>
        {
            using (events.CreateTransaction())
            {
                events.Publish(new TxAsyncEvent(7));
            } // <-- synchronous Dispose: commits, then blocks this single thread until handlers finish
        });

        var completed = await Task.WhenAny(work, Task.Delay(8000));
        completed.Should().BeSameAs(work,
            "synchronous transaction Dispose must not deadlock when a SynchronizationContext is present");
        await work; // surface any handler exception

        h.Recorder.Log.Should().Contain("async:7");
    }
}

/// <summary>
/// Minimal single-threaded SynchronizationContext (one pump thread) used to reproduce
/// sync-over-async deadlocks the way a Blazor Server circuit or a UI thread would.
/// </summary>
internal sealed class SingleThreadSyncContext : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly Thread _thread;

    public SingleThreadSyncContext()
    {
        _thread = new Thread(Pump) { IsBackground = true, Name = "single-thread-synccontext" };
        _thread.Start();
    }

    public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

    /// <summary>Posts an action onto the context thread; the returned task completes when it finishes.</summary>
    public Task Run(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(_ =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        }, null);
        return tcs.Task;
    }

    private void Pump()
    {
        SetSynchronizationContext(this);
        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            callback(state);
    }

    public void Dispose() => _queue.CompleteAdding();
}
