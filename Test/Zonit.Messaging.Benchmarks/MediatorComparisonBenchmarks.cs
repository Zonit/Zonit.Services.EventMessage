using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Messaging.Commands;

namespace Zonit.Messaging.Benchmarks;

// Head-to-head request/response dispatch against the two best-known in-process mediators.
// All three are wired into the SAME ServiceProvider and measured on the same machine in one run,
// so the numbers are directly comparable (cross-machine blog numbers are not).

// --- MediatR (reflection-based dispatch) ---
public sealed record MediatrCommand(int Value) : MediatR.IRequest<int>;

public sealed class MediatrCommandHandler : MediatR.IRequestHandler<MediatrCommand, int>
{
    public Task<int> Handle(MediatrCommand request, CancellationToken ct) => Task.FromResult(request.Value);
}

// --- martinothamar/Mediator (source-generated dispatch, like Zonit) ---
public sealed record MediatorCommand(int Value) : Mediator.IRequest<int>;

public sealed class MediatorCommandHandler : Mediator.IRequestHandler<MediatorCommand, int>
{
    public ValueTask<int> Handle(MediatorCommand request, CancellationToken ct) => ValueTask.FromResult(request.Value);
}

[MemoryDiagnoser]
public class MediatorComparisonBenchmarks
{
    private ServiceProvider _sp = null!;
    private ICommandProvider _zonit = null!;
    private MediatR.IMediator _mediatr = null!;
    private Mediator.IMediator _mediator = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();

        // Zonit (source-gen switch)
        services.AddCommandHandlers();

        // MediatR (reflection)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatrCommand).Assembly));

        // martinothamar/Mediator (source-gen)
        services.AddMediator();

        _sp = services.BuildServiceProvider();
        _zonit = _sp.GetRequiredService<ICommandProvider>();
        _mediatr = _sp.GetRequiredService<MediatR.IMediator>();
        _mediator = _sp.GetRequiredService<Mediator.IMediator>();
    }

    [GlobalCleanup]
    public void Cleanup() => _sp.Dispose();

    [Benchmark(Baseline = true)]
    public Task<int> Zonit_Send() => _zonit.SendAsync(new BenchCommand(1));

    [Benchmark]
    public Task<int> MediatR_Send() => _mediatr.Send(new MediatrCommand(1));

    [Benchmark]
    public ValueTask<int> Mediator_Send() => _mediator.Send(new MediatorCommand(1));
}
