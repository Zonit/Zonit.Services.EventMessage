namespace Zonit.Messaging.Commands;

/// <summary>
/// Provider wysy³aj¹cy requesty do odpowiednich handlerów.
/// Dzia³a jak dispatcher - znajduje handler dla danego requestu i wywo³uje go.
/// </summary>
public interface ICommandProvider
{
    /// <summary>
    /// Wysy³a request do odpowiedniego handlera i zwraca odpowiedŸ.
    /// Typ odpowiedzi jest inferowany z typu requestu.
    /// </summary>
    /// <typeparam name="TResponse">Typ odpowiedzi (inferowany z IRequest&lt;TResponse&gt;, automatycznie nullable)</typeparam>
    /// <param name="request">Request do wys³ania</param>
    /// <param name="cancellationToken">Token anulowania</param>
    /// <returns>OdpowiedŸ z handlera (mo¿e byæ null)</returns>
    Task<TResponse?> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : notnull;
}
