using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Zonit.Services.EventMessage;
using Zonit.Services.EventMessage.Services;

namespace Zonit.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventMessageService(this IServiceCollection services)
    {
        services.AddSingleton<IEventProvider, EventBusService>();
        services.AddHostedService<EventHandlersHostedService>();
        services.AddEventMessageHandlers();

        return services;
    }

    public static IServiceCollection AddEventMessageHandlers(this IServiceCollection services)
    {
        var loadedAssemblies = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

        // Załaduj brakujące zestawy, jeśli nie zostały automatycznie wykryte
        foreach (var assembly in loadedAssemblies.ToList())
        {
            try
            {
                foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
                {
                    if (!loadedAssemblies.Any(a => a.FullName == referencedAssembly.FullName))
                    {
                        var loaded = Assembly.Load(referencedAssembly);
                        loadedAssemblies.Add(loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd ładowania zależności: {ex.Message}");
            }
        }

        var handlerTypes = loadedAssemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null)!;
                }
            })
            .Where(t => t is not null && typeof(IEventHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Distinct();

        foreach (var type in handlerTypes)
        {
            if (type != null && !services.Any(s => s.ServiceType == typeof(IEventHandler) && s.ImplementationType == type))
            {
                services.AddScoped(typeof(IEventHandler), type);
                services.AddScoped(type);
            }
        }

        return services;
    }


}