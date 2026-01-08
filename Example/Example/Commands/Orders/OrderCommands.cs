namespace Example.Commands.Orders;

using Zonit.Messaging.Commands;

// ===== CREATE ORDER =====

/// <summary>
/// Command tworz¹cy nowe zamówienie.
/// Zwraca OrderCreatedResponse z pe³nymi informacjami.
/// </summary>
public record CreateOrderCommand(
    Guid CustomerId,
    List<OrderItemDto> Items
) : IRequest<OrderCreatedResponse>;

public record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice);

public record OrderCreatedResponse(
    Guid OrderId,
    string OrderNumber,
    decimal TotalAmount,
    DateTime CreatedAt
);

/// <summary>
/// Handler dla CreateOrderCommand.
/// </summary>
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderCreatedResponse>
{
    public async Task<OrderCreatedResponse> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        var orderId = Guid.NewGuid();
        var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{orderId.ToString()[..8].ToUpper()}";
        var totalAmount = command.Items.Sum(x => x.Quantity * x.UnitPrice);

        Console.WriteLine($"[CreateOrder] Created order {orderNumber} for customer {command.CustomerId}");
        Console.WriteLine($"[CreateOrder] Items: {command.Items.Count}, Total: {totalAmount:C}");

        return new OrderCreatedResponse(
            OrderId: orderId,
            OrderNumber: orderNumber,
            TotalAmount: totalAmount,
            CreatedAt: DateTime.UtcNow
        );
    }
}

// ===== CANCEL ORDER =====

/// <summary>
/// Wynik anulowania zamówienia.
/// </summary>
public enum CancelOrderResult
{
    Success,
    OrderNotFound,
    AlreadyCancelled,
    AlreadyShipped,
    AlreadyDelivered
}

/// <summary>
/// Command anuluj¹cy zamówienie.
/// </summary>
public record CancelOrderCommand(
    Guid OrderId,
    string Reason
) : IRequest<CancelOrderResult>;

/// <summary>
/// Handler dla CancelOrderCommand.
/// </summary>
public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResult>
{
    public async Task<CancelOrderResult> HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        // Symulacja - jeœli OrderId zaczyna siê od "00000", to ju¿ wys³ane
        if (command.OrderId.ToString().StartsWith("00000"))
            return CancelOrderResult.AlreadyShipped;

        Console.WriteLine($"[CancelOrder] Cancelled order {command.OrderId}. Reason: {command.Reason}");

        return CancelOrderResult.Success;
    }
}

// ===== ADD ORDER ITEM =====

/// <summary>
/// Command dodaj¹cy produkt do zamówienia.
/// Zwraca now¹ ³¹czn¹ kwotê zamówienia.
/// </summary>
public record AddOrderItemCommand(
    Guid OrderId,
    Guid ProductId,
    int Quantity
) : IRequest<decimal>;

/// <summary>
/// Handler dla AddOrderItemCommand.
/// </summary>
public class AddOrderItemCommandHandler : IRequestHandler<AddOrderItemCommand, decimal>
{
    public async Task<decimal> HandleAsync(AddOrderItemCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(30, cancellationToken);

        // Symulacja pobrania ceny produktu i obliczenia nowej sumy
        var productPrice = 29.99m;
        var newTotal = productPrice * command.Quantity + 100m; // 100 = istniej¹ce produkty

        Console.WriteLine($"[AddOrderItem] Added {command.Quantity}x product {command.ProductId} to order {command.OrderId}");
        Console.WriteLine($"[AddOrderItem] New total: {newTotal:C}");

        return newTotal;
    }
}
