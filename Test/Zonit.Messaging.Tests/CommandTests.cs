using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Messaging.Commands;

namespace Zonit.Messaging.Tests;

public sealed record Ping(string Text) : IRequest<string>;

public sealed class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string?> HandleAsync(Ping request, CancellationToken ct = default)
        => Task.FromResult<string?>($"pong:{request.Text}");
}

public sealed record Sum(int A, int B) : IRequest<int>;

public sealed class SumHandler : IRequestHandler<Sum, int>
{
    // For a value-type response, TResponse? collapses to TResponse (no struct constraint), so the
    // signature is Task<int>, not Task<int?>.
    public Task<int> HandleAsync(Sum request, CancellationToken ct = default)
        => Task.FromResult(request.A + request.B);
}

/// <summary>A request with no registered handler.</summary>
public sealed record Unhandled : IRequest<string>;

public class CommandTests
{
    [Fact]
    public async Task SendAsync_returns_handler_result()
    {
        await using var h = await TestHost.StartAsync(s => s.AddCommandHandlers());

        var result = await h.Get<ICommandProvider>().SendAsync(new Ping("hi"));

        result.Should().Be("pong:hi");
    }

    [Fact]
    public async Task SendAsync_supports_value_type_responses()
    {
        await using var h = await TestHost.StartAsync(s => s.AddCommandHandlers());

        var result = await h.Get<ICommandProvider>().SendAsync(new Sum(2, 3));

        result.Should().Be(5);
    }

    [Fact]
    public async Task SendAsync_with_no_handler_throws()
    {
        await using var h = await TestHost.StartAsync(s => s.AddCommandHandlers());

        var act = async () => await h.Get<ICommandProvider>().SendAsync(new Unhandled());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Manual_registration_works()
    {
        await using var h = await TestHost.StartAsync(s => s.AddCommand<PingHandler, Ping, string>());

        var result = await h.Get<ICommandProvider>().SendAsync(new Ping("manual"));

        result.Should().Be("pong:manual");
    }
}
