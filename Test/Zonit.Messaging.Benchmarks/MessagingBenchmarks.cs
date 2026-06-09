using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zonit.Messaging.Commands;
using Zonit.Messaging.Events;
using Zonit.Messaging.Tasks;

namespace Zonit.Messaging.Benchmarks;

public sealed record BenchCommand(int Value) : IRequest<int>;

public sealed class BenchCommandHandler : IRequestHandler<BenchCommand, int>
{
    public Task<int> HandleAsync(BenchCommand request, CancellationToken ct = default)
        => Task.FromResult(request.Value);
}

public sealed record BenchEvent(int Id);

public sealed class BenchEventHandler : IEventHandler<BenchEvent>
{
    public Task HandleAsync(BenchEvent data, CancellationToken ct) => Task.CompletedTask;
}

public sealed record BenchTask(int Id);

public sealed class BenchTaskHandler : ITaskHandler<BenchTask>
{
    public Task HandleAsync(TaskPayload<BenchTask> payload) => Task.CompletedTask;
}

[MemoryDiagnoser]
public class MessagingBenchmarks
{
    private ServiceProvider _sp = null!;
    private List<IHostedService> _hosted = null!;
    private ICommandProvider _commands = null!;
    private IEventProvider _events = null!;
    private IEventManager _eventManager = null!;
    private ITaskProvider _tasks = null!;
    private string _eventName = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddCommandHandlers();
        services.AddEventHandlers();
        services.AddTaskHandlers();

        _sp = services.BuildServiceProvider();
        _hosted = _sp.GetServices<IHostedService>().ToList();
        foreach (var h in _hosted)
            h.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        _commands = _sp.GetRequiredService<ICommandProvider>();
        _events = _sp.GetRequiredService<IEventProvider>();
        _eventManager = _sp.GetRequiredService<IEventManager>();
        _tasks = _sp.GetRequiredService<ITaskProvider>();
        _eventName = typeof(BenchEvent).FullName!;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var h in _hosted)
            h.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _sp.Dispose();
    }

    /// <summary>Fire-and-forget enqueue cost (the cheap path).</summary>
    [Benchmark]
    public void EventPublish() => _events.Publish(new BenchEvent(1));

    /// <summary>Request/response dispatch through the generated switch.</summary>
    [Benchmark]
    public Task<int> CommandSend() => _commands.SendAsync(new BenchCommand(1));

    /// <summary>Task enqueue + state creation.</summary>
    [Benchmark]
    public void TaskPublish() => _tasks.Publish(new BenchTask(1));

    /// <summary>Full in-line dispatch: fresh DI scope + GetServices + handler invocation
    /// (the per-message dispatch path the audit flagged).</summary>
    [Benchmark]
    public Task EventDispatch() => _eventManager.PublishAndWaitAsync(_eventName, new BenchEvent(1));
}
