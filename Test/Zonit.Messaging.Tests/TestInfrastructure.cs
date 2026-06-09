using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Zonit.Messaging.Tests;

/// <summary>
/// Per-test shared state. Registered as a singleton, so handlers (resolved from scopes off the
/// root provider) all observe the same instance, while each test stays isolated by its own
/// <see cref="ServiceProvider"/>.
/// </summary>
public sealed class Recorder
{
    public ConcurrentQueue<string> Log { get; } = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _signals = new();
    private int _concurrency;

    /// <summary>Highest number of handler bodies observed running at the same time.</summary>
    public int MaxConcurrency;

    public TaskCompletionSource Signal(string key)
        => _signals.GetOrAdd(key, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

    public Task WaitAsync(string key, int timeoutMs = 5000)
        => Signal(key).Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));

    /// <summary>Tracks concurrent execution; dispose to leave. Used to detect overlap.</summary>
    public IDisposable Enter()
    {
        var now = Interlocked.Increment(ref _concurrency);
        int observed;
        while ((observed = Volatile.Read(ref MaxConcurrency)) < now &&
               Interlocked.CompareExchange(ref MaxConcurrency, now, observed) != observed) { }
        return new Leaver(this);
    }

    private sealed class Leaver(Recorder r) : IDisposable
    {
        public void Dispose() => Interlocked.Decrement(ref r._concurrency);
    }
}

/// <summary>Builds a service provider, registers a <see cref="Recorder"/>, and starts hosted services.</summary>
public static class TestHost
{
    public static async Task<Harness> StartAsync(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        configure(services);

        var sp = services.BuildServiceProvider();
        var hosted = sp.GetServices<IHostedService>().ToList();
        foreach (var h in hosted)
            await h.StartAsync(CancellationToken.None);

        return new Harness(sp, hosted);
    }
}

public sealed class Harness(ServiceProvider services, IReadOnlyList<IHostedService> hosted) : IAsyncDisposable
{
    public ServiceProvider Services => services;
    public Recorder Recorder => services.GetRequiredService<Recorder>();
    public T Get<T>() where T : notnull => services.GetRequiredService<T>();

    /// <summary>Polls until <paramref name="condition"/> is true or the timeout elapses.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline)
                throw new TimeoutException("Condition was not met within the timeout.");
            await Task.Delay(15);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var h in hosted)
        {
            try { await h.StopAsync(CancellationToken.None); } catch { /* shutdown best-effort */ }
        }
        await services.DisposeAsync();
    }
}
