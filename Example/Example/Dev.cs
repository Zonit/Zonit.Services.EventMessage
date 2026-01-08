namespace Example;

using Zonit.Messaging.Commands;

/// <summary>
/// Prosty przykład użycia ICommandProvider.
/// Dla pełnych przykładów CQRS zobacz: CqrsDemo.cs
/// </summary>
internal class Dev
{
    private readonly ICommandProvider _commandProvider;

    public Dev(ICommandProvider commandProvider)
    {
        _commandProvider = commandProvider;
    }

    /// <summary>
    /// Demonstracja podstawowego użycia.
    /// </summary>
    public async Task RunAsync()
    {
        Console.WriteLine("=== Basic Command Provider Usage ===\n");

        // 1. Command zwracający obiekt
        var ping = await _commandProvider.SendAsync(new PingCommand());
        Console.WriteLine($"Ping response: {ping.Message} at {ping.Timestamp:T}");

        // 2. Command zwracający Guid
        Guid id = await _commandProvider.SendAsync(new GenerateIdCommand());
        Console.WriteLine($"Generated ID: {id}");

        // 3. Command zwracający bool
        bool valid = await _commandProvider.SendAsync(new ValidateEmailCommand("test@example.com"));
        Console.WriteLine($"Email valid: {valid}");

        // 4. Command zwracający enum
        var status = await _commandProvider.SendAsync(new GetStatusCommand());
        Console.WriteLine($"Status: {status}");

        Console.WriteLine("\n=== Done ===");
    }
}

// ===== SIMPLE EXAMPLES =====

public record PingCommand() : IRequest<PingResponse>;
public record PingResponse(string Message, DateTime Timestamp);

public class PingCommandHandler : IRequestHandler<PingCommand, PingResponse>
{
    public Task<PingResponse> HandleAsync(PingCommand request, CancellationToken ct = default)
        => Task.FromResult(new PingResponse("Pong!", DateTime.UtcNow));
}

public record GenerateIdCommand() : IRequest<Guid>;

public class GenerateIdCommandHandler : IRequestHandler<GenerateIdCommand, Guid>
{
    public Task<Guid> HandleAsync(GenerateIdCommand request, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());
}

public record ValidateEmailCommand(string Email) : IRequest<bool>;

public class ValidateEmailCommandHandler : IRequestHandler<ValidateEmailCommand, bool>
{
    public Task<bool> HandleAsync(ValidateEmailCommand request, CancellationToken ct = default)
        => Task.FromResult(request.Email.Contains('@') && request.Email.Contains('.'));
}

public enum ServiceStatus { Online, Maintenance, Offline }
public record GetStatusCommand() : IRequest<ServiceStatus>;

public class GetStatusCommandHandler : IRequestHandler<GetStatusCommand, ServiceStatus>
{
    public Task<ServiceStatus> HandleAsync(GetStatusCommand request, CancellationToken ct = default)
        => Task.FromResult(ServiceStatus.Online);
}

