using Zonit.Messaging.Tasks;

namespace Example.Demo;

// ============================================================================
// TASKS - Background Jobs pattern
// D³ugo trwaj¹ce zadania z retry, timeout i œledzeniem extensionId
// ============================================================================

#region Tasks

/// <summary>
/// Zadanie generowania raportu.
/// </summary>
public record GenerateReportTask(string ReportName, DateTime From, DateTime To);

/// <summary>
/// Zadanie wysy³ki emaila.
/// </summary>
public record SendEmailTask(string To, string Subject, string Body);

/// <summary>
/// Zadanie przetwarzania pliku.
/// </summary>
public record ProcessFileTask(string FileName, long FileSize);

#endregion

#region Demo

/// <summary>
/// Demonstracja wzorca Tasks (Background Jobs).
/// </summary>
public static class TasksDemo
{
    private static bool _handlersSubscribed = false;
    private static int _processedTasks = 0;

    public static async Task RunAsync(ITaskManager taskManager, ITaskProvider taskProvider)
    {
        try { Console.Clear(); } catch { }
        Console.WriteLine("????????????????????????????????????????????????????????????????");
        Console.WriteLine("?              TASKS DEMO - Background Jobs                    ?");
        Console.WriteLine("????????????????????????????????????????????????????????????????");
        Console.WriteLine();

        // Subskrybuj handlery (tylko raz)
        if (!_handlersSubscribed)
        {
            SubscribeHandlers(taskManager);
            _handlersSubscribed = true;
            Console.WriteLine("? Handlery zarejestrowane.\n");
        }

        while (true)
        {
            Console.WriteLine("Wybierz akcjê:");
            Console.WriteLine("  1. Dodaj zadanie: Generuj raport");
            Console.WriteLine("  2. Dodaj zadanie: Wyœlij email");
            Console.WriteLine("  3. Dodaj zadanie: Przetwórz plik");
            Console.WriteLine("  4. Dodaj wiele zadañ naraz");
            Console.WriteLine("  5. Poka¿ statystyki");
            Console.WriteLine("  0. Powrót do menu g³ównego");
            Console.WriteLine();
            Console.Write("Wybór: ");

            var choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await PublishGenerateReportAsync(taskProvider);
                    break;
                case "2":
                    await PublishSendEmailAsync(taskProvider);
                    break;
                case "3":
                    await PublishProcessFileAsync(taskProvider);
                    break;
                case "4":
                    await PublishMultipleTasksAsync(taskProvider);
                    break;
                case "5":
                    ShowStats();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Nieprawid³owy wybór.\n");
                    break;
            }

            // Oczekiwanie na przetworzenie zadañ
            await Task.Delay(1000);
        }
    }

    private static void SubscribeHandlers(ITaskManager taskManager)
    {
        // Handler dla generowania raportów
        taskManager.Subscribe<GenerateReportTask>(async payload =>
        {
            var task = payload.Data;
            Console.WriteLine($"   [Task] ?? Generujê raport: {task.ReportName}");
            Console.WriteLine($"   [Task]    Okres: {task.From:d} - {task.To:d}");
            
            // Symulacja d³ugiego przetwarzania
            await Task.Delay(1500);
            
            Console.WriteLine($"   [Task] ? Raport {task.ReportName} wygenerowany!");
            Interlocked.Increment(ref _processedTasks);
        }, new TaskSubscriptionOptions 
        { 
            WorkerCount = 2,
            Timeout = TimeSpan.FromMinutes(5)
        });

        // Handler dla wysy³ki emaili
        taskManager.Subscribe<SendEmailTask>(async payload =>
        {
            var task = payload.Data;
            Console.WriteLine($"   [Task] ?? Wysy³am email do: {task.To}");
            Console.WriteLine($"   [Task]    Temat: {task.Subject}");
            
            await Task.Delay(500);
            
            Console.WriteLine($"   [Task] ? Email wys³any do {task.To}!");
            Interlocked.Increment(ref _processedTasks);
        });

        // Handler dla przetwarzania plików
        taskManager.Subscribe<ProcessFileTask>(async payload =>
        {
            var task = payload.Data;
            Console.WriteLine($"   [Task] ?? Przetwarzam plik: {task.FileName}");
            Console.WriteLine($"   [Task]    Rozmiar: {task.FileSize / 1024.0:F2} KB");
            
            // Symulacja przetwarzania zale¿nego od rozmiaru
            await Task.Delay((int)(task.FileSize / 1000));
            
            Console.WriteLine($"   [Task] ? Plik {task.FileName} przetworzony!");
            Interlocked.Increment(ref _processedTasks);
            
            // SprawdŸ ExtensionId z metadanych
            if (payload.ExtensionId.HasValue)
            {
                Console.WriteLine($"   [Task]    ExtensionId: {payload.ExtensionId}");
            }
        }, new TaskSubscriptionOptions
        {
            MaxRetries = 3,
            RetryDelay = TimeSpan.FromSeconds(1)
        });
    }

    private static Task PublishGenerateReportAsync(ITaskProvider taskProvider)
    {
        Console.Write("Nazwa raportu: ");
        var name = Console.ReadLine() ?? "Raport sprzeda¿y";

        Console.WriteLine("\n? Dodajê zadanie GenerateReportTask...");
        taskProvider.Publish(new GenerateReportTask(
            name, 
            DateTime.Now.AddMonths(-1), 
            DateTime.Now));
        Console.WriteLine("? Zadanie dodane do kolejki.\n");
        
        return Task.CompletedTask;
    }

    private static Task PublishSendEmailAsync(ITaskProvider taskProvider)
    {
        Console.Write("Adres email: ");
        var to = Console.ReadLine() ?? "user@example.com";
        
        Console.Write("Temat: ");
        var subject = Console.ReadLine() ?? "Witamy!";

        Console.WriteLine("\n? Dodajê zadanie SendEmailTask...");
        taskProvider.Publish(new SendEmailTask(to, subject, "Treœæ wiadomoœci..."));
        Console.WriteLine("? Zadanie dodane do kolejki.\n");
        
        return Task.CompletedTask;
    }

    private static Task PublishProcessFileAsync(ITaskProvider taskProvider)
    {
        Console.Write("Nazwa pliku: ");
        var fileName = Console.ReadLine() ?? "dokument.pdf";
        
        Console.Write("Rozmiar (KB): ");
        var input = Console.ReadLine();
        var sizeKb = long.TryParse(input, out var s) ? s : 100;

        Console.WriteLine("\n? Dodajê zadanie ProcessFileTask z ExtensionId...");
        
        // Publikacja z ExtensionId - identyfikuje modu³ który wys³a³ zadanie
        var extensionId = Guid.NewGuid();
        taskProvider.Publish(new ProcessFileTask(fileName, sizeKb * 1024), extensionId);
        
        Console.WriteLine($"? Zadanie dodane do kolejki (ExtensionId: {extensionId:N}).\n");
        
        return Task.CompletedTask;
    }

    private static Task PublishMultipleTasksAsync(ITaskProvider taskProvider)
    {
        Console.Write("Ile zadañ wys³aæ? ");
        var input = Console.ReadLine();
        var count = int.TryParse(input, out var c) ? c : 5;

        Console.WriteLine($"\n? Dodajê {count} zadañ do kolejki...\n");
        
        for (int i = 1; i <= count; i++)
        {
            taskProvider.Publish(new SendEmailTask(
                $"user{i}@example.com", 
                $"Wiadomoœæ #{i}", 
                "Treœæ..."));
        }
        
        Console.WriteLine($"? Dodano {count} zadañ do kolejki.\n");
        return Task.CompletedTask;
    }

    private static void ShowStats()
    {
        Console.WriteLine("?????????????????????????????????");
        Console.WriteLine("?         STATYSTYKI            ?");
        Console.WriteLine("?????????????????????????????????");
        Console.WriteLine($"?  Przetworzone zadania: {_processedTasks,5}  ?");
        Console.WriteLine("?????????????????????????????????");
        Console.WriteLine();
    }
}

#endregion
