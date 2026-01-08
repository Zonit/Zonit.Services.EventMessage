using Zonit.Messaging.Commands;

namespace Example.Demo;

// ============================================================================
// COMMANDS - Request/Response pattern (CQRS)
// Ka¿dy Command/Query ma swój Handler i zwraca konkretny typ odpowiedzi
// ============================================================================

#region Commands & Queries

/// <summary>
/// Command do tworzenia u¿ytkownika.
/// Zwraca Guid nowo utworzonego u¿ytkownika.
/// </summary>
public record CreateUserCommand(string Name, string Email) : IRequest<Guid>;

/// <summary>
/// Query do pobrania u¿ytkownika.
/// Zwraca UserDto lub null jeœli nie znaleziono.
/// </summary>
public record GetUserQuery(Guid UserId) : IRequest<UserDto?>;

/// <summary>
/// Command do usuniêcia u¿ytkownika.
/// Zwraca true jeœli usuniêto, false jeœli nie znaleziono.
/// </summary>
public record DeleteUserCommand(Guid UserId) : IRequest<bool>;

/// <summary>
/// DTO u¿ytkownika.
/// </summary>
public record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt);

#endregion

#region Handlers

/// <summary>
/// Handler dla CreateUserCommand.
/// W prawdziwej aplikacji zapisa³by do bazy danych.
/// </summary>
public class CreateUserHandler : IRequestHandler<CreateUserCommand, Guid>
{
    // Symulacja bazy danych (w pamiêci)
    public static readonly Dictionary<Guid, UserDto> Users = new();

    public Task<Guid> HandleAsync(CreateUserCommand request, CancellationToken cancellationToken = default)
    {
        var userId = Guid.NewGuid();
        var user = new UserDto(userId, request.Name, request.Email, DateTime.UtcNow);
        Users[userId] = user;
        
        Console.WriteLine($"   [Handler] Utworzono u¿ytkownika: {user.Name} ({user.Email})");
        return Task.FromResult(userId);
    }
}

/// <summary>
/// Handler dla GetUserQuery.
/// </summary>
public class GetUserHandler : IRequestHandler<GetUserQuery, UserDto?>
{
    public Task<UserDto?> HandleAsync(GetUserQuery request, CancellationToken cancellationToken = default)
    {
        CreateUserHandler.Users.TryGetValue(request.UserId, out var user);
        Console.WriteLine(user != null 
            ? $"   [Handler] Znaleziono: {user.Name}" 
            : "   [Handler] U¿ytkownik nie znaleziony");
        return Task.FromResult(user);
    }
}

/// <summary>
/// Handler dla DeleteUserCommand.
/// </summary>
public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, bool>
{
    public Task<bool> HandleAsync(DeleteUserCommand request, CancellationToken cancellationToken = default)
    {
        var removed = CreateUserHandler.Users.Remove(request.UserId);
        Console.WriteLine(removed 
            ? "   [Handler] U¿ytkownik usuniêty" 
            : "   [Handler] U¿ytkownik nie istnieje");
        return Task.FromResult(removed);
    }
}

#endregion

#region Demo

/// <summary>
/// Demonstracja wzorca Commands (CQRS).
/// </summary>
public static class CommandsDemo
{
    public static async Task RunAsync(ICommandProvider commandProvider)
    {
        try { Console.Clear(); } catch { }
        Console.WriteLine("????????????????????????????????????????????????????????????????");
        Console.WriteLine("?           COMMANDS DEMO - Request/Response (CQRS)            ?");
        Console.WriteLine("????????????????????????????????????????????????????????????????");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Wybierz akcjê:");
            Console.WriteLine("  1. Utwórz u¿ytkownika");
            Console.WriteLine("  2. Pobierz u¿ytkownika");
            Console.WriteLine("  3. Usuñ u¿ytkownika");
            Console.WriteLine("  4. Poka¿ wszystkich u¿ytkowników");
            Console.WriteLine("  0. Powrót do menu g³ównego");
            Console.WriteLine();
            Console.Write("Wybór: ");

            var choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await CreateUserAsync(commandProvider);
                    break;
                case "2":
                    await GetUserAsync(commandProvider);
                    break;
                case "3":
                    await DeleteUserAsync(commandProvider);
                    break;
                case "4":
                    ShowAllUsers();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Nieprawid³owy wybór.\n");
                    break;
            }
        }
    }

    private static async Task CreateUserAsync(ICommandProvider commandProvider)
    {
        Console.Write("Podaj imiê: ");
        var name = Console.ReadLine() ?? "Jan";
        
        Console.Write("Podaj email: ");
        var email = Console.ReadLine() ?? "jan@example.com";

        Console.WriteLine("\n? Wysy³am CreateUserCommand...");
        var userId = await commandProvider.SendAsync(new CreateUserCommand(name, email));
        
        Console.WriteLine($"? U¿ytkownik utworzony z ID: {userId}\n");
    }

    private static async Task GetUserAsync(ICommandProvider commandProvider)
    {
        Console.Write("Podaj ID u¿ytkownika (lub Enter dla pierwszego): ");
        var input = Console.ReadLine();
        
        Guid userId;
        if (string.IsNullOrEmpty(input))
        {
            userId = CreateUserHandler.Users.Keys.FirstOrDefault();
            if (userId == Guid.Empty)
            {
                Console.WriteLine("Brak u¿ytkowników.\n");
                return;
            }
        }
        else if (!Guid.TryParse(input, out userId))
        {
            Console.WriteLine("Nieprawid³owy format ID.\n");
            return;
        }

        Console.WriteLine("\n? Wysy³am GetUserQuery...");
        var user = await commandProvider.SendAsync(new GetUserQuery(userId));
        
        if (user != null)
        {
            Console.WriteLine($"? Znaleziono: {user.Name} ({user.Email})");
            Console.WriteLine($"  Utworzono: {user.CreatedAt:yyyy-MM-dd HH:mm:ss}\n");
        }
        else
        {
            Console.WriteLine("? U¿ytkownik nie znaleziony.\n");
        }
    }

    private static async Task DeleteUserAsync(ICommandProvider commandProvider)
    {
        Console.Write("Podaj ID u¿ytkownika do usuniêcia: ");
        var input = Console.ReadLine();
        
        if (!Guid.TryParse(input, out var userId))
        {
            Console.WriteLine("Nieprawid³owy format ID.\n");
            return;
        }

        Console.WriteLine("\n? Wysy³am DeleteUserCommand...");
        var deleted = await commandProvider.SendAsync(new DeleteUserCommand(userId));
        
        Console.WriteLine(deleted 
            ? "? U¿ytkownik usuniêty.\n" 
            : "? U¿ytkownik nie istnieje.\n");
    }

    private static void ShowAllUsers()
    {
        Console.WriteLine("Wszyscy u¿ytkownicy:");
        if (CreateUserHandler.Users.Count == 0)
        {
            Console.WriteLine("  (brak)\n");
            return;
        }

        foreach (var user in CreateUserHandler.Users.Values)
        {
            Console.WriteLine($"  • {user.Id}: {user.Name} ({user.Email})");
        }
        Console.WriteLine();
    }
}

#endregion
