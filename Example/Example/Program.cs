using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zonit.Services;
using Zonit.Services.EventMessage;
using Serilog;
using Serilog.Events;

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
                services.AddEventMessageService(); // <-- Add this line!

                services.AddSerilog((services, lc) => lc
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console());

            }).Build();

        builder.RunAsync();

        var eventBus = builder.Services.GetRequiredService<IEventProvider>();

        //for (var i = 0; i <= 10000; i++)
        //    eventBus.Publish("Article.Created", new Test1("Title", $"{i}"));

        Console.Write("Enter text to publish: ");
        string? textVariable;
        while (!string.IsNullOrEmpty(textVariable = Console.ReadLine()))
        {
            eventBus.Publish("Article.Created", new Test1("Title", textVariable));
            //Console.Write("Enter text to publish: ");
        }

    }

    public record class Test1(string Name, string Context);
}