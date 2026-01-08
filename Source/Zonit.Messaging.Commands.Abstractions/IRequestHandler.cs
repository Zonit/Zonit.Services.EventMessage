namespace Zonit.Messaging.Commands;

/// <summary>
/// Handler dla konkretnego typu requestu.
/// Kaødy request powinien mieÊ dok≥adnie jeden handler.
/// </summary>
/// <typeparam name="TRequest">Typ requestu implementujπcego IRequest&lt;TResponse&gt;</typeparam>
/// <typeparam name="TResponse">Typ odpowiedzi</typeparam>
public interface IRequestHandler<TRequest, TResponse> 
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Obs≥uguje request i zwraca odpowiedü.
    /// </summary>
    /// <param name="request">Request do obs≥uøenia</param>
    /// <param name="cancellationToken">Token anulowania</param>
    /// <returns>Odpowiedü z handlera</returns>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
