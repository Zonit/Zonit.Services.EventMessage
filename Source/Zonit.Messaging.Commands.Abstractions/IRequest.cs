namespace Zonit.Messaging.Commands;

/// <summary>
/// Marker interface - request okreœla typ response.
/// Ka¿dy request mo¿e zwróciæ wartoœæ lub null (Guid, bool, obiekt, enum, etc.)
/// </summary>
/// <typeparam name="TResponse">Typ odpowiedzi zwracanej przez handler (automatycznie nullable)</typeparam>
public interface IRequest<TResponse> where TResponse : notnull
{
}
