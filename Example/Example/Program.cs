using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zonit.Services;
using Zonit.Services.EventMessage;
using Serilog;
using Serilog.Events;
using Example.Models;

namespace Example;

internal class Program
{
    static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) =>
            {
                services.AddEventMessageService(); // <-- Dodane zarejestrowanie serwisu

                services.AddSerilog((services, lc) => lc
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console());
            })
            .Build();

        // Uruchomienie hosta asynchronicznie
        var host = builder.RunAsync();


        var eventProvider = builder.Services.GetRequiredService<IEventProvider>();
        var eventProvider2 = builder.Services.GetRequiredService<IEventProvider>();

        using (var transaction = eventProvider.Transaction())
        {
            eventProvider2.Publish(new Test1Model { Title = "Test" });
            eventProvider2.Publish(new Test2Model { Title = "Test" });
            eventProvider2.Publish(new Test3Model { Title = "Test" });
            eventProvider2.Publish(new Test4Model { Title = "Test" });
            eventProvider2.Publish(new Test5Model { Title = "Test" });
        }

        Console.WriteLine("Start");
        Thread.Sleep(Timeout.Infinite);

        return;

        // Rejestrowanie wydarzeń
        var taskManager = builder.Services.GetRequiredService<ITaskManager>();

        // Subskrypcja zmian
        taskManager.EventOnChange(async taskManager =>
        {
            Console.WriteLine($"[Task] {taskManager.Id} {taskManager.Status}");

            var article = taskManager.Payload.Data as Article;
            if (article is not null)
                Console.WriteLine($"[Article] Title: {article.Title}");

            await Task.CompletedTask;
        });

        // Wysyłanie zadania
        var taskProvider = builder.Services.GetRequiredService<ITaskProvider>();

        string? textVariable;
        //while (!string.IsNullOrEmpty(textVariable = Console.ReadLine()))
        //{
        //    taskProvider.Publish(new Article { Title = textVariable });

        //    foreach (var task in taskManager.GetActiveTasks())
        //        Console.WriteLine($"[Aktywne zadanie] {task.Id} {task.Status}");
        //}


        //return;

        var eventBus = builder.Services.GetRequiredService<IEventProvider>();

        Console.Write("Enter text to publish: ");
        while (!string.IsNullOrEmpty(textVariable = Console.ReadLine()))
        {
            eventBus.Publish("Article.Created", new Test1("Title", textVariable));
            eventBus.Publish(new Article { Title = textVariable });
        }
    }

    public record class Test1(string Name, string Context);
}
