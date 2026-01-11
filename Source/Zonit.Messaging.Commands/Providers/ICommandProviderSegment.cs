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
    /// <returns>The response if handled, null if this segment cannot handle the request</returns>
    Task<(bool Handled, TResponse? Result)> TryHandleAsync<TResponse>(
        IRequest<TResponse> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
        where TResponse : notnull;
}
