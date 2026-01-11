using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Messaging.Commands;

/// <summary>
/// Composite command provider that delegates to registered segments.
/// Each assembly with command handlers contributes a segment.
/// This is 100% AOT-safe - no reflection at runtime.
/// </summary>
public sealed class CompositeCommandProvider : ICommandProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandProviderSegment[] _segments;

    public CompositeCommandProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _segments = serviceProvider.GetServices<ICommandProviderSegment>().ToArray();
    }

    public async Task<TResponse?> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var segment in _segments)
        {
            var (handled, result) = await segment.TryHandleAsync(request, _serviceProvider, cancellationToken);
            if (handled)
            {
                return result;
            }
        }

        throw new InvalidOperationException(
            $"No handler registered for request type '{request.GetType().FullName}'. " +
            $"Expected response type: '{typeof(TResponse).FullName}'. " +
            "Ensure handler is registered using AddCommandHandlers() or AddCommand<THandler>().");
    }
}
