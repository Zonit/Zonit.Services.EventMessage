namespace Zonit.Messaging.Commands;

/// <summary>
/// Provider wysy≥ajπcy requesty do odpowiednich handlerÛw.
/// Dzia≥a jak dispatcher - znajduje handler dla danego requestu i wywo≥uje go.
/// </summary>
public interface ICommandProvider
{
    /// <summary>
    /// Wysy≥a request do odpowiedniego handlera i zwraca odpowiedü.
    /// Typ odpowiedzi jest inferowany z typu requestu.
    /// </summary>
    /// <typeparam name="TResponse">Typ odpowiedzi (inferowany z IRequest&lt;TResponse&gt;)</typeparam>
    /// <param name="request">Request do wys≥ania</param>
    /// <param name="cancellationToken">Token anulowania</param>
    /// <returns>Odpowiedü z handlera</returns>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
