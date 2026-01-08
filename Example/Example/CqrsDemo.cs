namespace Example;

using Example.Commands.Users;
using Example.Commands.Orders;
using Example.Queries.Users;
using Example.Queries.Orders;
using Zonit.Messaging.Commands;

/// <summary>
/// Demonstracja wzorca CQRS z Zonit.Messaging.Commands.
/// 
/// CQRS (Command Query Responsibility Segregation):
/// - Commands: Modyfikuj¹ stan systemu, zwracaj¹ wynik operacji
/// - Queries: Odczytuj¹ dane, nie modyfikuj¹ stanu
/// </summary>
public class CqrsDemo
{
    private readonly ICommandProvider _commandProvider;

    public CqrsDemo(ICommandProvider commandProvider)
    {
        _commandProvider = commandProvider;
    }

    /// <summary>
    /// Przyk³ad kompletnego flow u¿ytkownika: rejestracja, aktualizacja, usuniêcie.
    /// </summary>
    public async Task UserManagementFlowAsync()
    {
        Console.WriteLine("\n========== USER MANAGEMENT FLOW ==========\n");

        // 1. CREATE - Utworzenie u¿ytkownika (zwraca Guid)
        Console.WriteLine("1. Creating new user...");
        Guid userId = await _commandProvider.SendAsync(
            new CreateUserCommand("john_doe", "john@example.com", "SecurePass123!"));
        Console.WriteLine($"   ? User created with ID: {userId}\n");

        // 2. READ - Pobranie u¿ytkownika (Query)
        Console.WriteLine("2. Fetching user details...");
        UserDto? user = await _commandProvider.SendAsync(
            new GetUserByIdQuery(userId));
        if (user != null)
        {
            Console.WriteLine($"   ? Found: {user.UserName} ({user.Email})");
            Console.WriteLine($"   ? Created: {user.CreatedAt:g}, Active: {user.IsActive}\n");
        }

        // 3. UPDATE - Aktualizacja u¿ytkownika (zwraca enum z wynikiem)
        Console.WriteLine("3. Updating user email...");
        UpdateUserResult updateResult = await _commandProvider.SendAsync(
            new UpdateUserCommand(userId, null, "john.doe.new@example.com"));
        Console.WriteLine($"   ? Update result: {updateResult}\n");

        // 4. CHANGE PASSWORD - Zmiana has³a (zwraca enum)
        Console.WriteLine("4. Changing password...");
        ChangePasswordResult passwordResult = await _commandProvider.SendAsync(
            new ChangePasswordCommand(userId, "OldPass123!", "NewSecurePass456!"));
        Console.WriteLine($"   ? Password change result: {passwordResult}\n");

        // 5. DELETE - Usuniêcie u¿ytkownika (zwraca bool)
        Console.WriteLine("5. Deleting user...");
        bool deleted = await _commandProvider.SendAsync(
            new DeleteUserCommand(userId));
        Console.WriteLine($"   ? User deleted: {deleted}\n");
    }

    /// <summary>
    /// Przyk³ad flow zamówienia: utworzenie, dodanie produktu, anulowanie.
    /// </summary>
    public async Task OrderManagementFlowAsync()
    {
        Console.WriteLine("\n========== ORDER MANAGEMENT FLOW ==========\n");

        var customerId = Guid.NewGuid();

        // 1. CREATE ORDER - Zwraca pe³ny obiekt response
        Console.WriteLine("1. Creating new order...");
        var orderResponse = await _commandProvider.SendAsync(
            new CreateOrderCommand(customerId, new List<OrderItemDto>
            {
                new(Guid.NewGuid(), 2, 29.99m),
                new(Guid.NewGuid(), 1, 49.99m),
                new(Guid.NewGuid(), 3, 9.99m)
            }));
        Console.WriteLine($"   ? Order created: {orderResponse.OrderNumber}");
        Console.WriteLine($"   ? Total: {orderResponse.TotalAmount:C}");
        Console.WriteLine($"   ? Created at: {orderResponse.CreatedAt:g}\n");

        // 2. ADD ITEM - Zwraca now¹ sumê zamówienia (decimal)
        Console.WriteLine("2. Adding item to order...");
        decimal newTotal = await _commandProvider.SendAsync(
            new AddOrderItemCommand(orderResponse.OrderId, Guid.NewGuid(), 2));
        Console.WriteLine($"   ? New order total: {newTotal:C}\n");

        // 3. GET ORDER DETAILS - Query
        Console.WriteLine("3. Fetching order details...");
        var orderDetails = await _commandProvider.SendAsync(
            new GetOrderByIdQuery(orderResponse.OrderId));
        if (orderDetails != null)
        {
            Console.WriteLine($"   ? Order: {orderDetails.OrderNumber}");
            Console.WriteLine($"   ? Status: {orderDetails.Status}");
            Console.WriteLine($"   ? Items: {orderDetails.Items.Count}");
            Console.WriteLine($"   ? Total with tax: {orderDetails.Total:C}\n");
        }

        // 4. CANCEL ORDER - Zwraca enum z wynikiem
        Console.WriteLine("4. Cancelling order...");
        CancelOrderResult cancelResult = await _commandProvider.SendAsync(
            new CancelOrderCommand(orderResponse.OrderId, "Customer changed their mind"));
        Console.WriteLine($"   ? Cancel result: {cancelResult}\n");
    }

    /// <summary>
    /// Przyk³ad ró¿nych typów queries.
    /// </summary>
    public async Task QueryExamplesAsync()
    {
        Console.WriteLine("\n========== QUERY EXAMPLES ==========\n");

        // 1. SIMPLE QUERY - Sprawdzenie czy email istnieje (zwraca bool)
        Console.WriteLine("1. Checking if email exists...");
        bool exists = await _commandProvider.SendAsync(
            new CheckUserExistsQuery("existing.user@example.com"));
        Console.WriteLine($"   ? Email exists: {exists}\n");

        // 2. LIST QUERY - Pobranie listy z paginacj¹
        Console.WriteLine("2. Fetching users list...");
        var usersList = await _commandProvider.SendAsync(
            new GetUsersListQuery(Page: 1, PageSize: 10, SearchTerm: "User"));
        Console.WriteLine($"   ? Found: {usersList.TotalCount} users");
        Console.WriteLine($"   ? Page: {usersList.Page}/{usersList.TotalPages}");
        Console.WriteLine($"   ? Showing: {usersList.Users.Count} users\n");

        // 3. FILTERED QUERY - Zamówienia klienta
        Console.WriteLine("3. Fetching customer orders...");
        var customerOrders = await _commandProvider.SendAsync(
            new GetCustomerOrdersQuery(
                CustomerId: Guid.NewGuid(),
                Status: OrderStatus.Delivered,
                Page: 1,
                PageSize: 5));
        Console.WriteLine($"   ? Orders: {customerOrders.TotalCount}");
        Console.WriteLine($"   ? Total spent: {customerOrders.TotalSpent:C}\n");

        // 4. STATISTICS QUERY - Agregacje
        Console.WriteLine("4. Fetching order statistics...");
        var stats = await _commandProvider.SendAsync(
            new GetOrderStatisticsQuery(
                FromDate: DateTime.UtcNow.AddMonths(-1),
                ToDate: DateTime.UtcNow));
        Console.WriteLine($"   ? Total orders: {stats.TotalOrders}");
        Console.WriteLine($"   ? Completed: {stats.CompletedOrders}");
        Console.WriteLine($"   ? Revenue: {stats.TotalRevenue:C}");
        Console.WriteLine($"   ? Average order: {stats.AverageOrderValue:C}\n");
    }

    /// <summary>
    /// Uruchomienie wszystkich przyk³adów.
    /// </summary>
    public async Task RunAllExamplesAsync()
    {
        Console.WriteLine("?????????????????????????????????????????????????????????");
        Console.WriteLine("?     ZONIT.MESSAGING.COMMANDS - CQRS DEMO              ?");
        Console.WriteLine("?????????????????????????????????????????????????????????");

        await UserManagementFlowAsync();
        await OrderManagementFlowAsync();
        await QueryExamplesAsync();

        Console.WriteLine("?????????????????????????????????????????????????????????");
        Console.WriteLine("?                    DEMO COMPLETED                     ?");
        Console.WriteLine("?????????????????????????????????????????????????????????");
    }
}
