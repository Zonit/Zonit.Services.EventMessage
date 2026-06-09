# Commands (CQRS)

A command is a request handled by exactly **one** handler that returns a typed response. Use it
for request/response within a process (create a user and get its id, run a query and get a result).
Dispatch is a generated `switch` over concrete request types — AOT-safe, no reflection.

## Define a request + handler

The request implements `IRequest<TResponse>`; the handler implements
`IRequestHandler<TRequest, TResponse>`:

```csharp
public record CreateUser(string Name, string Email) : IRequest<Guid>;

public sealed class CreateUserHandler : IRequestHandler<CreateUser, Guid>
{
    private readonly IUserRepository _users;
    public CreateUserHandler(IUserRepository users) => _users = users;

    public async Task<Guid> HandleAsync(CreateUser request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await _users.AddAsync(id, request.Name, request.Email, cancellationToken);
        return id;
    }
}
```

Register with `services.AddCommandHandlers();`.

Queries are just commands that don't mutate state — the same `IRequest<T>` / `IRequestHandler<,>`
pair:

```csharp
public record GetUser(Guid Id) : IRequest<UserDto>;

public sealed class GetUserHandler : IRequestHandler<GetUser, UserDto>
{
    public Task<UserDto> HandleAsync(GetUser request, CancellationToken ct = default) => /* ... */;
}
```

## Send

Resolve `ICommandProvider` and `SendAsync`. The response type is inferred from the request:

```csharp
public sealed class Onboarding(ICommandProvider commands)
{
    public async Task<Guid> RunAsync(string name, string email)
    {
        Guid userId = await commands.SendAsync(new CreateUser(name, email));
        return userId;
    }
}
```

If no handler is registered for the request type, `SendAsync` throws — a missing handler is a
wiring bug, surfaced loudly rather than silently ignored.

## One handler per request

Unlike events, a command has a single handler. Registering two handlers for the same
`IRequest<T>` is a configuration error. Reach for [events](./events.md) when you want fan-out.
