namespace Zonit.Services.EventMessage;

/// <summary>
/// Model ładunku z danymi dla handlerów
/// </summary>
/// <typeparam name="T">Typ danych ładunku</typeparam>
public class PayloadModel<T>
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
    public PayloadModel(T data, CancellationToken cancellationToken = default)
    {
        Data = data;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Dane ładunku
    /// </summary>
    public required T Data { get; init; }

    /// <summary>
    /// Token anulowania dla operacji
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
}

/// <summary>
/// Nietypowany model ładunku jako klasa pochodna dla zgodności wstecznej
/// </summary>
public class PayloadModel : PayloadModel<object>
{
    /// <summary>
    /// Konstruktor domyślny
    /// </summary>
    public PayloadModel() : base()
    {
    }

    /// <summary>
    /// Konstruktor z parametrami
    /// </summary>
    /// <param name="data">Dane modelu</param>
    /// <param name="cancellationToken">Token anulowania</param>
    public PayloadModel(object data, CancellationToken cancellationToken = default)
        : base(data, cancellationToken)
    {
    }
}