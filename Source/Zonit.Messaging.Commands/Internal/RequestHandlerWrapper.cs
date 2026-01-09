namespace Zonit.Messaging.Commands;

/// <summary>
/// Wewnêtrzny interfejs wrappera - ukrywa generyki dla DI.
/// </summary>
internal interface IRequestHandlerWrapper<TResponse> where TResponse : notnull
{
    Task<TResponse?> HandleAsync(object request, CancellationToken cancellationToken);
}

/// <summary>
/// Wrapper opakowuj¹cy handler - pozwala na type-safe wywo³anie z object request.
/// </summary>
internal sealed class RequestHandlerWrapper<TRequest, TResponse> : IRequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : notnull
{
    private readonly IRequestHandler<TRequest, TResponse> _handler;

    public RequestHandlerWrapper(IRequestHandler<TRequest, TResponse> handler)
    {
        _handler = handler;
    }

    public Task<TResponse?> HandleAsync(object request, CancellationToken cancellationToken)
        => _handler.HandleAsync((TRequest)request, cancellationToken);
}
