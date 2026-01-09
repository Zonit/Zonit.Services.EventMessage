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

/// <summary>
/// Zadanie importu danych z postêpem.
/// </summary>
public record ImportDataTask(string Source, int RecordCount);

#endregion

#region Task Handlers with Progress

/// <summary>
/// Handler importu danych z postêpem - demonstracja TaskHandler.
/// </summary>
public class ImportDataTaskHandler : TaskHandler<ImportDataTask>
{
    public override int WorkerCount => 2;
    public override TimeSpan Timeout => TimeSpan.FromMinutes(5);

    public override TaskProgressStep[]? ProgressSteps =>
    [
        new(TimeSpan.FromSeconds(10), "Laczenie ze zrodlem..."),
        new(TimeSpan.FromSeconds(15), "Pobieranie danych..."),
        new(TimeSpan.FromSeconds(5), "Walidacja rekordow..."),
        new(TimeSpan.FromSeconds(10), "Zapisywanie do bazy...")
    ];

    protected override async Task HandleAsync(
        ImportDataTask data,
        ITaskProgressContext progress,
        CancellationToken cancellationToken)
    {
        // Krok 1: Laczenie
        await progress.NextAsync();
        await Task.Delay(10000, cancellationToken);

        // Krok 2: Pobieranie
        await progress.NextAsync();
        await Task.Delay(15000, cancellationToken);

        // Krok 3: Walidacja
        await progress.NextAsync();
        var batchSize = Math.Max(1, data.RecordCount / 10); // 10 aktualizacji
        for (int i = 0; i < data.RecordCount; i += batchSize)
        {
            await Task.Delay(400, cancellationToken); // 4s total dla 10 batchy
            await progress.SetMessageAsync($"Walidacja rekordow {Math.Min(i + batchSize, data.RecordCount)}/{data.RecordCount}...");
        }

        // Krok 4: Zapis
        await progress.NextAsync();
        await Task.Delay(10000, cancellationToken);
    }
}

#endregion

#region Demo

/// <summary>
/// Demonstracja wzorca Tasks (Background Jobs).
/// </summary>
public static class TasksDemo
{
    private static bool _handlersSubscribed = false;
    private static int _processedTasks = 0;
    private static IDisposable? _progressSubscription;
    private static readonly object _consoleLock = new();

    public static async Task RunAsync(ITaskManager taskManager, ITaskProvider taskProvider)
    {
        try { Console.Clear(); } catch { }
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine("|              TASKS DEMO - Background Jobs                    |");
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine();

        // Subskrybuj handlery (tylko raz)
        if (!_handlersSubscribed)
        {
            SubscribeHandlers(taskManager);
            SubscribeProgressMonitor(taskManager);
            _handlersSubscribed = true;
            Console.WriteLine("[OK] Handlery zarejestrowane.\n");
        }

        while (true)
        {
            Console.WriteLine("Wybierz akcje:");
            Console.WriteLine("  1. Dodaj zadanie: Generuj raport");
            Console.WriteLine("  2. Dodaj zadanie: Wyslij email");
            Console.WriteLine("  3. Dodaj zadanie: Przetworz plik");
            Console.WriteLine("  4. Dodaj wiele zadan naraz");
            Console.WriteLine("  5. [NOWE] Import danych (z progress barem!)");
            Console.WriteLine("  6. Pokaz statystyki");
            Console.WriteLine("  0. Powrot do menu glownego");
            Console.WriteLine();
            Console.Write("Wybor: ");

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
                    await PublishImportDataAsync(taskProvider);
                    break;
                case "6":
                    ShowStats(taskManager);
                    break;
                case "0":
                    _progressSubscription?.Dispose();
                    return;
                default:
                    Console.WriteLine("Nieprawidlowy wybor.\n");
                    break;
            }

            // Oczekiwanie na przetworzenie zadan
            await Task.Delay(500);
        }
    }

    private static void SubscribeProgressMonitor(ITaskManager taskManager)
    {
        // Generyczna subskrypcja dla ImportDataTask - pokazuje progress bar
        _progressSubscription = taskManager.OnChange<ImportDataTask>(state =>
        {
            lock (_consoleLock)
            {
                DrawProgressBar(state);
            }
        });
    }

    private static void DrawProgressBar(TaskState<ImportDataTask> state)
    {
        var progress = state.Progress ?? 0;
        var step = state.CurrentStep ?? 0;
        var totalSteps = state.TotalSteps ?? 0;
        var message = state.Message ?? "";
        var status = state.Status;

        // Progress bar
        const int barWidth = 40;
        var filledWidth = (int)(progress / 100.0 * barWidth);
        var emptyWidth = barWidth - filledWidth;

        var progressBar = new string('#', filledWidth) + new string('-', emptyWidth);

        // Status icon
        var statusIcon = status switch
        {
            Zonit.Messaging.Tasks.TaskStatus.Pending => "[...]",
            Zonit.Messaging.Tasks.TaskStatus.Processing => "[>>>]",
            Zonit.Messaging.Tasks.TaskStatus.Completed => "[OK!]",
            Zonit.Messaging.Tasks.TaskStatus.Failed => "[ERR]",
            Zonit.Messaging.Tasks.TaskStatus.Cancelled => "[---]",
            _ => "[???]"
        };

        // Wypisz progress bar
        Console.Write($"\r   {statusIcon} [{progressBar}] {progress,3}% ");
        
        if (totalSteps > 0)
        {
            Console.Write($"({step}/{totalSteps}) ");
        }
        
        // Truncate message if too long
        if (message.Length > 30)
        {
            message = message[..27] + "...";
        }
        Console.Write($"{message,-35}");

        if (status is Zonit.Messaging.Tasks.TaskStatus.Completed or 
            Zonit.Messaging.Tasks.TaskStatus.Failed or 
            Zonit.Messaging.Tasks.TaskStatus.Cancelled)
        {
            Console.WriteLine();
            Console.WriteLine($"   [INFO] Import: {state.Data.Source} - {state.Data.RecordCount} rekordow");
            Console.WriteLine();
            Interlocked.Increment(ref _processedTasks);
        }
    }

    private static void SubscribeHandlers(ITaskManager taskManager)
    {
        // Handler dla generowania raportow
        taskManager.Subscribe<GenerateReportTask>(async payload =>
        {
            var task = payload.Data;
            Console.WriteLine($"   [Task] Generuje raport: {task.ReportName}");
            Console.WriteLine($"   [Task]    Okres: {task.From:d} - {task.To:d}");
            
            // Symulacja dlugiego przetwarzania
            await Task.Delay(1500);
            
            Console.WriteLine($"   [Task] OK - Raport {task.ReportName} wygenerowany!");
            Interlocked.Increment(ref _processedTasks);
        }, new TaskSubscriptionOptions 
        { 
            WorkerCount = 2,
            Timeout = TimeSpan.FromMinutes(5)
        });

        // Handler dla wysylki emaili
        taskManager.Subscribe<SendEmailTask>(async payload =>
        {
            var task = payload.Data;
            Console.WriteLine($"   [Task] Wysylam email do: {task.To}");
            Console.WriteLine($"   [Task]    Temat: {task.Subject}");
            
            await Task.Delay(500);
            
            Console.WriteLine($"   [Task] OK - Email wyslany do {task.To}!");
            Interlocked.Increment(ref _processedTasks);
        });

        // Handler dla przetwarzania plikow
        taskManager.Subscribe<ProcessFileTask>(async payload =>
        {
            var task = payload.Data;
            Console.WriteLine($"   [Task] Przetwarzam plik: {task.FileName}");
            Console.WriteLine($"   [Task]    Rozmiar: {task.FileSize / 1024.0:F2} KB");
            
            // Symulacja przetwarzania zaleznego od rozmiaru
            await Task.Delay((int)(task.FileSize / 1000));
            
            Console.WriteLine($"   [Task] OK - Plik {task.FileName} przetworzony!");
            Interlocked.Increment(ref _processedTasks);
            
            // Sprawdz ExtensionId z metadanych
            if (payload.ExtensionId.HasValue)
            {
                Console.WriteLine($"   [Task]    ExtensionId: {payload.ExtensionId}");
            }
        }, new TaskSubscriptionOptions
        {
            MaxRetries = 3,
            RetryDelay = TimeSpan.FromSeconds(1)
        });

        // Handler dla importu danych z postepem
        // Tworzymy instancje handlera zeby pobrac ProgressSteps
        var importHandler = new ImportDataTaskHandler();
        taskManager.Subscribe<ImportDataTask>(async payload =>
        {
            // Uzyj handlera - Progress jest juz ustawiony przez system
            await ((ITaskHandler<ImportDataTask>)importHandler).HandleAsync(payload);
        }, new TaskSubscriptionOptions
        {
            WorkerCount = importHandler.WorkerCount,
            Timeout = importHandler.Timeout,
            ProgressSteps = importHandler.ProgressSteps
        });
    }

    private static Task PublishGenerateReportAsync(ITaskProvider taskProvider)
    {
        Console.Write("Nazwa raportu: ");
        var name = Console.ReadLine() ?? "Raport sprzedazy";

        Console.WriteLine("\n[+] Dodaje zadanie GenerateReportTask...");
        taskProvider.Publish(new GenerateReportTask(
            name, 
            DateTime.Now.AddMonths(-1), 
            DateTime.Now));
        Console.WriteLine("[OK] Zadanie dodane do kolejki.\n");
        
        return Task.CompletedTask;
    }

    private static Task PublishSendEmailAsync(ITaskProvider taskProvider)
    {
        Console.Write("Adres email: ");
        var to = Console.ReadLine() ?? "user@example.com";
        
        Console.Write("Temat: ");
        var subject = Console.ReadLine() ?? "Witamy!";

        Console.WriteLine("\n[+] Dodaje zadanie SendEmailTask...");
        taskProvider.Publish(new SendEmailTask(to, subject, "Tresc wiadomosci..."));
        Console.WriteLine("[OK] Zadanie dodane do kolejki.\n");
        
        return Task.CompletedTask;
    }

    private static Task PublishProcessFileAsync(ITaskProvider taskProvider)
    {
        Console.Write("Nazwa pliku: ");
        var fileName = Console.ReadLine() ?? "dokument.pdf";
        
        Console.Write("Rozmiar (KB): ");
        var input = Console.ReadLine();
        var sizeKb = long.TryParse(input, out var s) ? s : 100;

        Console.WriteLine("\n[+] Dodaje zadanie ProcessFileTask z ExtensionId...");
        
        // Publikacja z ExtensionId - identyfikuje modul ktory wyslal zadanie
        var extensionId = Guid.NewGuid();
        taskProvider.Publish(new ProcessFileTask(fileName, sizeKb * 1024), extensionId);
        
        Console.WriteLine($"[OK] Zadanie dodane do kolejki (ExtensionId: {extensionId:N}).\n");
        
        return Task.CompletedTask;
    }

    private static Task PublishMultipleTasksAsync(ITaskProvider taskProvider)
    {
        Console.Write("Ile zadan wyslac? ");
        var input = Console.ReadLine();
        var count = int.TryParse(input, out var c) ? c : 5;

        Console.WriteLine($"\n[+] Dodaje {count} zadan do kolejki...\n");
        
        for (int i = 1; i <= count; i++)
        {
            taskProvider.Publish(new SendEmailTask(
                $"user{i}@example.com", 
                $"Wiadomosc #{i}", 
                "Tresc..."));
        }
        
        Console.WriteLine($"[OK] Dodano {count} zadan do kolejki.\n");
        return Task.CompletedTask;
    }

    private static async Task PublishImportDataAsync(ITaskProvider taskProvider)
    {
        Console.Write("Zrodlo danych: ");
        var source = Console.ReadLine() ?? "database.csv";
        
        Console.Write("Liczba rekordow: ");
        var input = Console.ReadLine();
        var recordCount = int.TryParse(input, out var r) ? r : 500;

        Console.WriteLine("\n[+] Dodaje zadanie ImportDataTask z postepem...\n");
        
        taskProvider.Publish(new ImportDataTask(source, recordCount));
        
        // Czekaj na zakonczenie zeby zobaczyc progress bar
        await Task.Delay(12000);
    }

    private static void ShowStats(ITaskManager taskManager)
    {
        var activeTasks = taskManager.GetActiveTasks();
        
        Console.WriteLine("+-----------------------------------+");
        Console.WriteLine("|           STATYSTYKI              |");
        Console.WriteLine("+-----------------------------------+");
        Console.WriteLine($"|  Przetworzone zadania: {_processedTasks,8}  |");
        Console.WriteLine($"|  Aktywne zadania:      {activeTasks.Count,8}  |");
        Console.WriteLine("+-----------------------------------+");
        
        if (activeTasks.Count > 0)
        {
            Console.WriteLine("\nAktywne zadania:");
            foreach (var task in activeTasks)
            {
                var progress = task.Progress.HasValue ? $"{task.Progress}%" : "N/A";
                Console.WriteLine($"  - {task.TaskType}: {task.Status} ({progress})");
            }
        }
        Console.WriteLine();
    }
}

#endregion
