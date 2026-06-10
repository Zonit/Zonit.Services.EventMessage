namespace Zonit.Messaging.Commands;

/// <summary>
/// Represents a segment of command handling capability.
/// Each assembly with command handlers generates its own segment.
/// This is 100% AOT-safe - no reflection at runtime.
/// </summary>
public interface ICommandProviderSegment
{
    /// <summary>
    /// Attempts to handle the request if this segment knows how to handle it.
    /// </summary>
    /// <typeparam name="TResponse">The response type</typeparam>
    /// <param name="request">The request to handle</param>
    /// <param name="serviceProvider">Service provider for resolving handlers</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="result">The handler's task when handled; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if this segment handled the request.</returns>
    /// <remarks>
    /// Returns the handler's <see cref="Task{TResult}"/> directly (via an identity reference
    /// reinterpret — see the generated segment), with no boxing of the response and no tuple/state-machine
    /// allocation. Safe because <see cref="IRequest{TResponse}"/> is invariant, so a matched request's
    /// declared response type is exactly <typeparamref name="TResponse"/> at runtime.
    /// </remarks>
    bool TryHandle<TResponse>(
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        out Task<TResponse?> result)
        where TResponse : notnull;
}
