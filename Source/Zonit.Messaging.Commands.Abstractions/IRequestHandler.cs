namespace Zonit.Messaging.Commands;

/// <summary>
/// Handler dla konkretnego typu requestu.
/// Ka¿dy request powinien mieæ dok³adnie jeden handler.
/// </summary>
/// <typeparam name="TRequest">Typ requestu implementuj¹cego IRequest&lt;TResponse&gt;</typeparam>
/// <typeparam name="TResponse">Typ odpowiedzi (automatycznie nullable)</typeparam>
public interface IRequestHandler<TRequest, TResponse> 
    where TRequest : IRequest<TResponse>
    where TResponse : notnull
{
    /// <summary>
    /// Obs³uguje request i zwraca odpowiedŸ.
    /// </summary>
    /// <param name="request">Request do obs³u¿enia</param>
    /// <param name="cancellationToken">Token anulowania</param>
    /// <returns>OdpowiedŸ z handlera (mo¿e byæ null)</returns>
    Task<TResponse?> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
