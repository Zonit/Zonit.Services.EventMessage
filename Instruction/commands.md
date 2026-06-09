# Commands (CQRS)

A command is a request handled by exactly **one** handler that returns a typed response. Use it
for request/response within a process (create a user and get its id, run a query and get a result).
Dispatch is a generated `switch` over concrete request types, so it is AOT-safe with no reflection.

## Define a request + handler

The request implements `IRequest<TResponse>`; the handler implements
`IRequestHandler<TRequest, TResponse>`. The handler signature is `Task<TResponse?>`: for a
**reference-type** response that means a nullable return (`Task<UserDto?>`); for a **value-type**
response the annotation collapses, so it is just `Task<Guid>` / `Task<int>`.

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

Register with `services.AddCommandHandlers();` and the source generator discovers the handler.

Queries are just commands that don't mutate state, using the same `IRequest<T>` /
`IRequestHandler<,>` pair:

```csharp
public record GetUser(Guid Id) : IRequest<UserDto>;

public sealed class GetUserHandler : IRequestHandler<GetUser, UserDto>
{
    public Task<UserDto?> HandleAsync(GetUser request, CancellationToken ct = default) => /* ... */;
}
```

## Send

Resolve `ICommandProvider` and `SendAsync`. The response type is inferred from the request, and the
result is nullable:

```csharp
public sealed class Onboarding(ICommandProvider commands)
{
    public async Task<Guid> RunAsync(string name, string email)
        => await commands.SendAsync(new CreateUser(name, email));
}
```

If no handler is registered for the request type, `SendAsync` throws an `InvalidOperationException`
naming the request and expected response type; a missing handler is a wiring bug, surfaced loudly.

## One handler per request

Unlike events, a command has a single handler. Registering two handlers for the same
`IRequest<T>` is a configuration error. Reach for [events](./events.md) when you want fan-out.

## Manual registration

Auto-discovery (`AddCommandHandlers()`) is the norm. For explicit control, register one handler
(AOT-annotated, trimming-clean):

```csharp
services.AddCommand<CreateUserHandler, CreateUser, Guid>();
```

## API reference

### Contracts (`Zonit.Messaging.Commands.Abstractions`)

| Member | Signature | Notes |
|:---|:---|:---|
| `IRequest<TResponse>` | marker on the request type | `TResponse : notnull` |
| `IRequestHandler<TRequest, TResponse>.HandleAsync` | `Task<TResponse?> HandleAsync(TRequest request, CancellationToken ct = default)` | one handler per request |
| `ICommandProvider.SendAsync` | `Task<TResponse?> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)` | dispatches to the handler |

### Registration (`Zonit.Messaging.Commands`)

| Method | Purpose |
|:---|:---|
| `AddCommandHandlers()` | register the dispatcher + all source-generated handlers (call once per assembly) |
| `AddCommand<THandler, TRequest, TResponse>()` | manually register one handler (AOT-safe) |
