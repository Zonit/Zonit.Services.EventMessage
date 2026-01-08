namespace Example.Queries.Orders;

using Zonit.Messaging.Commands;

// ===== GET ORDER BY ID =====

/// <summary>
/// Query pobieraj¹cy szczegó³y zamówienia.
/// </summary>
public record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderDetailsDto?>;

/// <summary>
/// DTO szczegó³ów zamówienia.
/// </summary>
public record OrderDetailsDto(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    List<OrderItemDetailsDto> Items,
    decimal SubTotal,
    decimal Tax,
    decimal Total,
    OrderStatus Status,
    DateTime CreatedAt,
    DateTime? ShippedAt,
    DateTime? DeliveredAt
);

public record OrderItemDetailsDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice
);

public enum OrderStatus
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}

/// <summary>
/// Handler dla GetOrderByIdQuery.
/// </summary>
public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailsDto?>
{
    public async Task<OrderDetailsDto?> HandleAsync(GetOrderByIdQuery query, CancellationToken cancellationToken = default)
    {
        await Task.Delay(30, cancellationToken);

        if (query.OrderId == Guid.Empty)
        {
            Console.WriteLine($"[GetOrderById] Order not found: {query.OrderId}");
            return null;
        }

        Console.WriteLine($"[GetOrderById] Found order: {query.OrderId}");

        var items = new List<OrderItemDetailsDto>
        {
            new(Guid.NewGuid(), "Product A", 2, 29.99m, 59.98m),
            new(Guid.NewGuid(), "Product B", 1, 49.99m, 49.99m)
        };

        var subTotal = items.Sum(x => x.TotalPrice);
        var tax = subTotal * 0.23m;

        return new OrderDetailsDto(
            Id: query.OrderId,
            OrderNumber: $"ORD-{query.OrderId.ToString()[..8].ToUpper()}",
            CustomerId: Guid.NewGuid(),
            Items: items,
            SubTotal: subTotal,
            Tax: tax,
            Total: subTotal + tax,
            Status: OrderStatus.Processing,
            CreatedAt: DateTime.UtcNow.AddDays(-2),
            ShippedAt: null,
            DeliveredAt: null
        );
    }
}

// ===== GET CUSTOMER ORDERS =====

/// <summary>
/// Query pobieraj¹cy zamówienia klienta.
/// </summary>
public record GetCustomerOrdersQuery(
    Guid CustomerId,
    OrderStatus? Status,
    int Page,
    int PageSize
) : IRequest<CustomerOrdersResponse>;

/// <summary>
/// Response z zamówieniami klienta.
/// </summary>
public record CustomerOrdersResponse(
    List<OrderSummaryDto> Orders,
    int TotalCount,
    decimal TotalSpent
);

public record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    decimal Total,
    OrderStatus Status,
    DateTime CreatedAt
);

/// <summary>
/// Handler dla GetCustomerOrdersQuery.
/// </summary>
public class GetCustomerOrdersQueryHandler : IRequestHandler<GetCustomerOrdersQuery, CustomerOrdersResponse>
{
    public async Task<CustomerOrdersResponse> HandleAsync(GetCustomerOrdersQuery query, CancellationToken cancellationToken = default)
    {
        await Task.Delay(40, cancellationToken);

        // Symulacja danych
        var orders = Enumerable.Range(1, 15)
            .Select(i => new OrderSummaryDto(
                Id: Guid.NewGuid(),
                OrderNumber: $"ORD-{DateTime.UtcNow.AddDays(-i):yyyyMMdd}-{i:D4}",
                Total: 50m + (i * 25.5m),
                Status: (OrderStatus)(i % 6),
                CreatedAt: DateTime.UtcNow.AddDays(-i)
            ))
            .ToList();

        // Filtrowanie po statusie
        if (query.Status.HasValue)
        {
            orders = orders.Where(o => o.Status == query.Status.Value).ToList();
        }

        // Paginacja
        var paged = orders
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var totalSpent = orders.Sum(o => o.Total);

        Console.WriteLine($"[GetCustomerOrders] Found {orders.Count} orders for customer {query.CustomerId}");

        return new CustomerOrdersResponse(
            Orders: paged,
            TotalCount: orders.Count,
            TotalSpent: totalSpent
        );
    }
}

// ===== GET ORDER STATISTICS =====

/// <summary>
/// Query pobieraj¹cy statystyki zamówieñ.
/// </summary>
public record GetOrderStatisticsQuery(
    DateTime FromDate,
    DateTime ToDate
) : IRequest<OrderStatisticsDto>;

public record OrderStatisticsDto(
    int TotalOrders,
    int PendingOrders,
    int CompletedOrders,
    int CancelledOrders,
    decimal TotalRevenue,
    decimal AverageOrderValue
);

/// <summary>
/// Handler dla GetOrderStatisticsQuery.
/// </summary>
public class GetOrderStatisticsQueryHandler : IRequestHandler<GetOrderStatisticsQuery, OrderStatisticsDto>
{
    public async Task<OrderStatisticsDto> HandleAsync(GetOrderStatisticsQuery query, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        Console.WriteLine($"[GetOrderStatistics] Calculating stats from {query.FromDate:d} to {query.ToDate:d}");

        // Symulacja statystyk
        return new OrderStatisticsDto(
            TotalOrders: 1250,
            PendingOrders: 45,
            CompletedOrders: 1150,
            CancelledOrders: 55,
            TotalRevenue: 125000m,
            AverageOrderValue: 100m
        );
    }
}
