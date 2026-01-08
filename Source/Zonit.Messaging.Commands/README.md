# Zonit.Messaging.Commands

**Command/Query request-response pattern** dla Zonit Framework - minimalistyczna, type-safe implementacja z obs³ug¹ AOT/trimming.

## ?? Pakiety

| Pakiet | Opis |
|--------|------|
| **Zonit.Messaging.Commands.Abstractions** | Interfejsy: `IRequest<TResponse>`, `IRequestHandler<,>`, `ICommandProvider` |
| **Zonit.Messaging.Commands** | Implementacja: `CommandProvider`, extension methods dla DI |
| **Zonit.Messaging.Commands.SourceGenerators** | Source Generator dla AOT-safe rejestracji handlerów |

## ?? Szybki start

### 1. Instalacja

```bash
dotnet add package Zonit.Messaging.Commands
dotnet add package Zonit.Messaging.Commands.SourceGenerators
```

### 2. Definiowanie Request i Response

```csharp
using Zonit.Messaging.Commands;

// Request z pe³nym obiektem response
public record GetUserRequest(Guid Id) : IRequest<UserResponse>;

public record UserResponse(Guid Id, string UserName, string Email);

// Request zwracaj¹cy Guid (ID utworzonego zasobu)
public record CreateOrderRequest(Guid ProductId, int Quantity) : IRequest<Guid>;

// Request zwracaj¹cy bool (sukces/pora¿ka)
public record DeleteUserCommand(Guid UserId) : IRequest<bool>;

// Request zwracaj¹cy enum (szczegó³owy status)
public enum UpdateResult { Success, NotFound, ValidationError }
public record UpdateUserCommand(Guid UserId, string Email) : IRequest<UpdateResult>;
```

### 3. Tworzenie Handlera

```csharp
public class GetUserRequestHandler : IRequestHandler<GetUserRequest, UserResponse>
{
    private readonly IUserRepository _repository;

    public GetUserRequestHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserResponse> HandleAsync(
        GetUserRequest request, 
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetByIdAsync(request.Id, cancellationToken);
        
        return new UserResponse(user.Id, user.UserName, user.Email);
    }
}
```

### 4. Rejestracja w DI

**Opcja A: Rêczna (z reflection, nie AOT-safe)**

```csharp
builder.Services.AddCommandProvider();
builder.Services.AddCommand<GetUserRequestHandler>();
builder.Services.AddCommand<CreateOrderRequestHandler>();
```

**Opcja B: Automatyczna (AOT-safe, Source Generator)** ?

```csharp
// Source Generator automatycznie znajdzie wszystkie handlery
builder.Services.AddCommandHandlers();
```

### 5. U¿ycie

```csharp
public class UserService
{
    private readonly ICommandProvider _commandProvider;

    public UserService(ICommandProvider commandProvider)
    {
        _commandProvider = commandProvider;
    }

    public async Task<UserResponse> GetUserAsync(Guid userId)
    {
        // Typ response jest inferowany z request - brak jawnych typów!
        var response = await _commandProvider.SendAsync(new GetUserRequest(userId));
        return response;
    }

    public async Task<Guid> CreateOrderAsync(Guid productId, int quantity)
    {
        var orderId = await _commandProvider.SendAsync(
            new CreateOrderRequest(productId, quantity));
        
        return orderId;
    }
}
```

## ?? Kluczowe cechy

### ? Type-safe
```csharp
var request = new GetUserRequest(userId);
UserResponse response = await provider.SendAsync(request); // ? Typ inferowany!
```

### ? Ka¿dy request MUSI zwracaæ wartoœæ
- **Guid** - ID utworzonego zasobu
- **bool** - sukces/pora¿ka operacji
- **enum** - szczegó³owy status (Success, NotFound, ValidationError, etc.)
- **record/class** - pe³ny obiekt z danymi

**Brak `Unit` type** - ka¿da operacja zwraca sensown¹ wartoœæ.

### ? AOT/Trimming compatible

**Source Generator generuje kod compile-time:**
```csharp
// Wygenerowane automatycznie:
public static class GeneratedCommandHandlerExtensions
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.TryAddScoped<ICommandProvider, GeneratedCommandProvider>();
        services.AddScoped<IRequestHandler<GetUserRequest, UserResponse>, GetUserRequestHandler>();
        services.AddScoped<IRequestHandler<CreateOrderRequest, Guid>, CreateOrderRequestHandler>();
        // ... wszystkie handlery
        return services;
    }
}
```

### ? SOLID-compliant
- **Open/Closed** - dodajesz nowe handlery bez modyfikowania CommandProvider
- **Single Responsibility** - ka¿dy handler obs³uguje jeden typ requestu
- **Dependency Inversion** - zale¿noœæ od abstrakcji (`ICommandProvider`, `IRequestHandler`)

## ?? Porównanie z innymi rozwi¹zaniami

| Cecha | Zonit.Messaging.Commands | MediatR | 
|-------|-------------------------|---------|
| **AOT/Trimming** | ? Full (z Source Generator) | ?? Partial |
| **Reflection runtime** | ? Nie (z SG) | ? Tak |
| **Type inference** | ? Pe³ne | ? Pe³ne |
| **Pipeline behaviors** | ? Planned | ? Tak |
| **Notifications** | ? Planned | ? Tak |
| **Waga biblioteki** | ?? Minimalna | ?? Œrednia |

## ?? Zaawansowane u¿ycie

### Lifetime handlerów
```csharp
// Domyœlnie: Scoped
services.AddCommand<MyHandler>();

// Singleton
services.AddCommand<MySingletonHandler>(ServiceLifetime.Singleton);

// Transient
services.AddCommand<MyTransientHandler>(ServiceLifetime.Transient);
```

### Dependency Injection w handlerach
```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderRequest, Guid>
{
    private readonly IOrderRepository _orders;
    private readonly ILogger<CreateOrderHandler> _logger;
    private readonly IEventPublisher _events;

    public CreateOrderHandler(
        IOrderRepository orders,
        ILogger<CreateOrderHandler> logger,
        IEventPublisher events)
    {
        _orders = orders;
        _logger = logger;
        _events = events;
    }

    public async Task<Guid> HandleAsync(CreateOrderRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Creating order for product {ProductId}", request.ProductId);
        
        var order = new Order(request.ProductId, request.Quantity);
        await _orders.AddAsync(order, ct);
        
        await _events.PublishAsync(new OrderCreatedEvent(order.Id), ct);
        
        return order.Id;
    }
}
```

## ??? Roadmap

- [ ] Pipeline behaviors (logging, validation, transactions)
- [ ] Telemetry/metrics integration
- [ ] Retry policies
- [ ] Query vs Command separation
- [ ] Streaming responses

## ?? Licencja

MIT

## ?? Powi¹zane projekty

- **Zonit.Services.EventMessage** - Event-driven messaging (pub/sub)
- **Zonit.Extensions** - Core extensions dla Zonit Framework
