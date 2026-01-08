using Zonit.Messaging.Events;

namespace Example.Demo;

// ============================================================================
// EVENTS - Pub/Sub pattern
// Jeden event mo¿e mieæ wielu subskrybentów (fan-out)
// ============================================================================

#region Events

/// <summary>
/// Event publikowany gdy u¿ytkownik siê zarejestrowa³.
/// </summary>
public record UserRegisteredEvent(Guid UserId, string Name, string Email);

/// <summary>
/// Event publikowany gdy zamówienie zosta³o z³o¿one.
/// </summary>
public record OrderPlacedEvent(Guid OrderId, Guid CustomerId, decimal Total);

/// <summary>
/// Event publikowany gdy wiadomoœæ zosta³a wys³ana.
/// </summary>
public record MessageSentEvent(string From, string To, string Content);

#endregion

#region Demo

/// <summary>
/// Demonstracja wzorca Events (Pub/Sub).
/// </summary>
public static class EventsDemo
{
    private static bool _handlersSubscribed = false;

    public static async Task RunAsync(IEventManager eventManager, IEventProvider eventProvider)
    {
        try { Console.Clear(); } catch { }
        Console.WriteLine("????????????????????????????????????????????????????????????????");
        Console.WriteLine("?              EVENTS DEMO - Pub/Sub Pattern                   ?");
        Console.WriteLine("????????????????????????????????????????????????????????????????");
        Console.WriteLine();

        // Subskrybuj handlery (tylko raz)
        if (!_handlersSubscribed)
        {
            SubscribeHandlers(eventManager);
            _handlersSubscribed = true;
            Console.WriteLine("? Handlery zarejestrowane.\n");
        }

        while (true)
        {
            Console.WriteLine("Wybierz akcjê:");
            Console.WriteLine("  1. Publikuj UserRegisteredEvent");
            Console.WriteLine("  2. Publikuj OrderPlacedEvent");
            Console.WriteLine("  3. Publikuj MessageSentEvent");
            Console.WriteLine("  4. Publikuj wiele eventów naraz");
            Console.WriteLine("  0. Powrót do menu g³ównego");
            Console.WriteLine();
            Console.Write("Wybór: ");

            var choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await PublishUserRegisteredAsync(eventProvider);
                    break;
                case "2":
                    await PublishOrderPlacedAsync(eventProvider);
                    break;
                case "3":
                    await PublishMessageSentAsync(eventProvider);
                    break;
                case "4":
                    await PublishMultipleEventsAsync(eventProvider);
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Nieprawid³owy wybór.\n");
                    break;
            }

            // Krótkie oczekiwanie na przetworzenie eventów
            await Task.Delay(500);
        }
    }

    private static void SubscribeHandlers(IEventManager eventManager)
    {
        // Handler 1: Logowanie rejestracji
        eventManager.Subscribe<UserRegisteredEvent>(async payload =>
        {
            Console.WriteLine($"   [Handler 1] ?? Wysy³am email powitalny do: {payload.Data.Email}");
            await Task.Delay(100); // Symulacja wysy³ki emaila
        });

        // Handler 2: Statystyki
        eventManager.Subscribe<UserRegisteredEvent>(async payload =>
        {
            Console.WriteLine($"   [Handler 2] ?? Aktualizujê statystyki dla: {payload.Data.Name}");
            await Task.Delay(50);
        });

        // Handler dla zamówieñ
        eventManager.Subscribe<OrderPlacedEvent>(async payload =>
        {
            Console.WriteLine($"   [Handler] ?? Zamówienie {payload.Data.OrderId:N} na kwotê {payload.Data.Total:C}");
            await Task.CompletedTask;
        });

        // Handler dla wiadomoœci
        eventManager.Subscribe<MessageSentEvent>(async payload =>
        {
            Console.WriteLine($"   [Handler] ?? Wiadomoœæ od {payload.Data.From} do {payload.Data.To}: \"{payload.Data.Content}\"");
            await Task.CompletedTask;
        });
    }

    private static Task PublishUserRegisteredAsync(IEventProvider eventProvider)
    {
        Console.Write("Podaj imiê: ");
        var name = Console.ReadLine() ?? "Jan";
        
        Console.Write("Podaj email: ");
        var email = Console.ReadLine() ?? "jan@example.com";

        Console.WriteLine("\n? Publikujê UserRegisteredEvent...");
        eventProvider.Publish(new UserRegisteredEvent(Guid.NewGuid(), name, email));
        Console.WriteLine("? Event opublikowany.\n");
        
        return Task.CompletedTask;
    }

    private static Task PublishOrderPlacedAsync(IEventProvider eventProvider)
    {
        Console.Write("Podaj kwotê zamówienia: ");
        var input = Console.ReadLine();
        var total = decimal.TryParse(input, out var t) ? t : 99.99m;

        Console.WriteLine("\n? Publikujê OrderPlacedEvent...");
        eventProvider.Publish(new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), total));
        Console.WriteLine("? Event opublikowany.\n");
        
        return Task.CompletedTask;
    }

    private static Task PublishMessageSentAsync(IEventProvider eventProvider)
    {
        Console.Write("Od kogo: ");
        var from = Console.ReadLine() ?? "Jan";
        
        Console.Write("Do kogo: ");
        var to = Console.ReadLine() ?? "Anna";
        
        Console.Write("Treœæ: ");
        var content = Console.ReadLine() ?? "Czeœæ!";

        Console.WriteLine("\n? Publikujê MessageSentEvent...");
        eventProvider.Publish(new MessageSentEvent(from, to, content));
        Console.WriteLine("? Event opublikowany.\n");
        
        return Task.CompletedTask;
    }

    private static Task PublishMultipleEventsAsync(IEventProvider eventProvider)
    {
        Console.WriteLine("? Publikujê 3 eventy jednoczeœnie...\n");
        
        eventProvider.Publish(new UserRegisteredEvent(Guid.NewGuid(), "Alicja", "alicja@example.com"));
        eventProvider.Publish(new OrderPlacedEvent(Guid.NewGuid(), Guid.NewGuid(), 299.99m));
        eventProvider.Publish(new MessageSentEvent("System", "Admin", "Nowa rejestracja"));
        
        Console.WriteLine("? Wszystkie eventy opublikowane.\n");
        return Task.CompletedTask;
    }
}

#endregion
