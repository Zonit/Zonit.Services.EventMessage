namespace Zonit.Messaging.Commands;

/// <summary>
/// Marker interface - request okreœla typ response.
/// Ka¿dy request musi zwracaæ sensown¹ wartoœæ (Guid, bool, obiekt, enum, etc.)
/// </summary>
/// <typeparam name="TResponse">Typ odpowiedzi zwracanej przez handler</typeparam>
public interface IRequest<TResponse>
{
}
