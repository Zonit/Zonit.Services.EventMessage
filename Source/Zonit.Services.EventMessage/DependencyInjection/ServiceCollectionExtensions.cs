using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Services.EventMessage;
using Zonit.Services.EventMessage.Services;

namespace Zonit.Services;

public static class ServiceCollectionExtensions
{
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
        services.AddHostedService<EventHandlersHostedService>();

        services.AddSingleton<ITaskProvider, TaskProviderService>();
        services.AddSingleton<ITaskManager, TaskManagerService>();
        services.AddHostedService<TaskHandlerRegistrationHostedService>();

        // Remove duplicate registration
        // services.AddHostedService<EventHandlersHostedService>();

        // Rejestracja handlerów na podstawie opcji
        if (options.AutoDiscoverHandlers)
        {
            services.AddEventMessageHandlers(options.AssembliesToScan?.ToArray());
            services.AddTaskHandlers(options.AssembliesToScan?.ToArray());
        }

        return services;
    }

    public static IServiceCollection AddEventMessageHandlers(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        // Jeśli nie podano żadnych assemblies, użyj aktualnie załadowanych
        var assembliesToScan = assemblies?.Length > 0
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        // Znajdź wszystkie typy implementujące IEventHandler
        var handlerTypes = assembliesToScan
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Zwróć tylko udane typy
                    return ex.Types.Where(t => t != null)!;
                }
                catch
                {
                    // W przypadku błędu, zwróć pustą listę
                    return Array.Empty<Type>();
                }
            })
            .Where(type =>
                type != null &&
                !type.IsAbstract &&
                !type.IsInterface &&
                typeof(IEventHandler).IsAssignableFrom(type))
            .Distinct();

        // Zarejestruj znalezione handlery
        foreach (var handlerType in handlerTypes)
        {
            // Sprawdź, czy handler nie jest już zarejestrowany
            if (!services.Any(s => s.ServiceType == typeof(IEventHandler) &&
                           s.ImplementationType == handlerType))
            {
                services.AddScoped(typeof(IEventHandler), handlerType);
                services.AddScoped(handlerType);
            }
        }

        return services;
    }

    public static IServiceCollection AddTaskHandlers(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        // Jeśli nie podano żadnych assemblies, użyj aktualnie załadowanych
        var assembliesToScan = assemblies?.Length > 0
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        // Znajdź wszystkie typy implementujące ITaskHandler (dziedziczące po TaskBase<T>)
        var handlerTypes = assembliesToScan
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Zwróć tylko udane typy
                    return ex.Types.Where(t => t != null)!;
                }
                catch
                {
                    // W przypadku błędu, zwróć pustą listę
                    return Array.Empty<Type>();
                }
            })
            .Where(type =>
                type != null &&
                !type.IsAbstract &&
                !type.IsInterface &&
                typeof(ITaskHandler).IsAssignableFrom(type) &&
                type.BaseType != null &&
                type.BaseType.IsGenericType &&
                type.BaseType.GetGenericTypeDefinition() == typeof(TaskBase<>))
            .Distinct();

        // Zarejestruj znalezione handlery zadań
        foreach (var handlerType in handlerTypes)
        {
            // Sprawdź, czy handler nie jest już zarejestrowany
            if (!services.Any(s => s.ServiceType == handlerType))
            {
                // Zarejestruj jako ITaskHandler
                services.AddScoped(typeof(ITaskHandler), handlerType);

                // Zarejestruj konkretną implementację
                services.AddScoped(handlerType);

                // Automatycznie wywołaj Subscribe dla wszystkich handlerów przy uruchomieniu
                services.AddTransient<ITaskHandlerInitializer>(provider =>
                    new TaskHandlerInitializer(provider, handlerType));
            }
        }

        return services;
    }
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

// Pomocniczy interfejs dla inicjalizacji handlerów
public interface ITaskHandlerInitializer
{
    void Initialize();
}

// Pomocnicza klasa do inicjalizacji handlerów
public class TaskHandlerInitializer : ITaskHandlerInitializer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Type _handlerType;

    public TaskHandlerInitializer(IServiceProvider serviceProvider, Type handlerType)
    {
        _serviceProvider = serviceProvider;
        _handlerType = handlerType;
    }

    public void Initialize()
    {
        var handler = _serviceProvider.GetRequiredService(_handlerType) as ITaskHandler;
        handler?.Subscribe(_serviceProvider);
    }
}