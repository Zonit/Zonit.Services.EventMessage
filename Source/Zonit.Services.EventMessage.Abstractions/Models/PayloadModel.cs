namespace Zonit.Services.EventMessage;

public class PayloadModel
{
    public required object Data { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}