namespace Zonit.Services.EventMessage;

public class PayloadModel
{
    /// <summary>
    /// Dane zdarzenia
    /// </summary>
    public required object Data { get; init; }

    /// <summary>
    /// Token anulowania dla operacji
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }
}

public class PayloadModel<TModel>
{
    /// <summary>
    /// Konstruktor domyślny
    /// </summary>
    public PayloadModel()
    {
    }

    /// <summary>
    /// Konstruktor z parametrami
    /// </summary>
    /// <param name="data">Dane modelu</param>
    /// <param name="cancellationToken">Token anulowania</param>
    public PayloadModel(TModel data, CancellationToken cancellationToken)
    {
        Data = data;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Dane zdarzenia
    /// </summary>
    public required TModel Data { get; init; }

    /// <summary>
    /// Token anulowania dla operacji
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }
}
