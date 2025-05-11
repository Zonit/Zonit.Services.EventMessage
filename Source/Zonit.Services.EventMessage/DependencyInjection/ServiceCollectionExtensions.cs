using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Services.EventMessage;
using Zonit.Services.EventMessage.Services;

namespace Zonit.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHandlers<THandler>(
      this IServiceCollection services,
      params Assembly[]? assemblies) where THandler : class, IHandler
    {
        // Jeśli nie podano żadnych assemblies, użyj aktualnie załadowanych
        var assembliesToScan = assemblies?.Length > 0
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        // Znajdź wszystkie typy implementujące THandler
        var handlerTypes = assembliesToScan
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null)!;
                }
                catch
                {
                    return [];
                }
            })
            .Where(type =>
                type != null &&
                !type.IsAbstract &&
                !type.IsInterface &&
                typeof(THandler).IsAssignableFrom(type))
            .Distinct();

        // Zarejestruj znalezione handlery
        foreach (var handlerType in handlerTypes)
        {
            // Sprawdź, czy handler nie jest już zarejestrowany
            if (!services.Any(s => s.ServiceType == typeof(THandler) && s.ImplementationType == handlerType) && handlerType is not null)
            {
                // Zarejestruj jako interfejs THandler
                services.AddScoped(typeof(THandler), handlerType);

                // Zarejestruj konkretną implementację
                services.AddScoped(handlerType);
            }
        }

        return services;
    }

    public static IServiceCollection AddEventMessageService(
        this IServiceCollection services,
        Action<EventMessageOptions>? configureOptions = null)
    {
        // Konfiguracja opcji
        var options = new EventMessageOptions();
        configureOptions?.Invoke(options);

        // Rejestracja głównych usług
        services.AddSingleton<IEventProvider, EventProviderService>();
        services.AddSingleton<IEventManager, EventManagerService>();
        services.AddSingleton<ITaskProvider, TaskProviderService>();
        services.AddSingleton<ITaskManager, TaskManagerService>();

        // Rejestracja uniwersalnych usług hostowanych
        services.AddHostedService<HandlerRegistrationHostedService<IEventHandler>>();
        services.AddHostedService<HandlerRegistrationHostedService<ITaskHandler>>();

        // Rejestracja handlerów na podstawie opcji
        if (options.AutoDiscoverHandlers)
        {
            services.AddHandlers<IEventHandler>(options.AssembliesToScan?.ToArray());
            services.AddHandlers<ITaskHandler>(options.AssembliesToScan?.ToArray());
        }

        return services;
    }

    public class EventMessageOptions
    {
        /// <summary>
        /// Określa, czy automatycznie wyszukiwać handlery
        /// </summary>
        public bool AutoDiscoverHandlers { get; set; } = true;

        /// <summary>
        /// Lista assemblies, które mają być przeskanowane w poszukiwaniu handlerów
        /// </summary>
        public List<Assembly>? AssembliesToScan { get; set; }
    }
}