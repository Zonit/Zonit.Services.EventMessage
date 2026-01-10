using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Zonit.Services.EventMessage;
using Zonit.Services.EventMessage.Services;

namespace Zonit.Services;

/// <summary>
/// [LEGACY] Extension methods for registering EventMessage services.
/// </summary>
/// <remarks>
/// <para><b>Te metody s¹ przestarza³e.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// Zamiast: <c>services.AddEventMessageService()</c><br/>
/// U¿yj osobno:<br/>
/// - <c>services.AddCommandProvider()</c> (z namespace Zonit.Messaging.Commands)<br/>
/// - <c>services.AddEventProvider()</c> (z namespace Zonit.Messaging.Events)<br/>
/// - <c>services.AddTaskProvider()</c> (z namespace Zonit.Messaging.Tasks)
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// [LEGACY] Rejestruje serwisy EventMessage (legacy API).
    /// </summary>
    /// <remarks>
    /// <para><b>Ta metoda jest przestarza³a.</b></para>
    /// <para>
    /// <b>Migracja:</b><br/>
    /// U¿yj osobno:<br/>
    /// - <c>services.AddCommandProvider()</c> dla CQRS<br/>
    /// - <c>services.AddEventProvider()</c> dla Pub/Sub<br/>
    /// - <c>services.AddTaskProvider()</c> dla Background Jobs
    /// </para>
    /// </remarks>
    [Obsolete("Use AddCommandProvider(), AddEventProvider(), AddTaskProvider() from Zonit.Messaging.* namespaces instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddEventMessageService(
        this IServiceCollection services,
        Action<EventMessageOptions>? configureOptions = null)
    {
        // Konfiguracja opcji
        var options = new EventMessageOptions();
        configureOptions?.Invoke(options);

        // Rejestracja nowych serwisów (Zonit.Messaging.*)
        services.AddSingleton<Zonit.Messaging.Events.IEventManager, Zonit.Messaging.Events.EventManager>();
        services.AddSingleton<Zonit.Messaging.Events.IEventProvider, Zonit.Messaging.Events.EventProvider>();
        services.AddSingleton<Zonit.Messaging.Tasks.ITaskManager, Zonit.Messaging.Tasks.TaskManager>();
        services.AddSingleton<Zonit.Messaging.Tasks.ITaskProvider, Zonit.Messaging.Tasks.TaskProvider>();

        // Rejestracja legacy wrapperów (Zonit.Services.EventMessage)
        services.AddSingleton<IEventProvider, LegacyEventProviderService>();
        services.AddSingleton<IEventManager, LegacyEventManagerService>();
        services.AddSingleton<ITaskProvider, LegacyTaskProviderService>();
        services.AddSingleton<ITaskManager, LegacyTaskManagerService>();

        // Rejestracja uniwersalnych us³ug hostowanych
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

    /// <summary>
    /// [LEGACY] Rejestruje handlery przez reflection.
    /// </summary>
    /// <remarks>
    /// <para><b>Ta metoda jest przestarza³a i nie jest AOT-safe.</b></para>
    /// <para>
    /// <b>Migracja:</b><br/>
    /// U¿yj Source Generator:<br/>
    /// - <c>services.AddCommandHandlers()</c><br/>
    /// - <c>services.AddEventHandlers()</c><br/>
    /// - <c>services.AddTaskHandlers()</c>
    /// </para>
    /// </remarks>
    [Obsolete("This method uses reflection and is not AOT-safe. Use AddCommandHandlers(), AddEventHandlers(), AddTaskHandlers() from Source Generators instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [RequiresUnreferencedCode("This method uses reflection to scan assemblies and is not compatible with trimming.")]
    [RequiresDynamicCode("This method uses reflection to create service registrations dynamically.")]
    public static IServiceCollection AddHandlers<THandler>(
        this IServiceCollection services,
        params Assembly[]? assemblies) where THandler : class, IHandler
    {
        // Jeœli nie podano ¿adnych assemblies, u¿yj aktualnie za³adowanych
        var assembliesToScan = assemblies?.Length > 0
            ? assemblies
            : AppDomain.CurrentDomain.GetAssemblies();

        // ZnajdŸ wszystkie typy implementuj¹ce THandler
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
            if (!services.Any(s => s.ServiceType == typeof(THandler) && s.ImplementationType == handlerType) && handlerType is not null)
            {
                services.AddScoped(typeof(THandler), handlerType);
                services.AddScoped(handlerType);
            }
        }

        return services;
    }

    /// <summary>
    /// [LEGACY] Opcje konfiguracji EventMessage.
    /// </summary>
    [Obsolete("Use specific options for Commands, Events, or Tasks instead.")]
    public class EventMessageOptions
    {
        /// <summary>
        /// Okreœla, czy automatycznie wyszukiwaæ handlery przez reflection.
        /// </summary>
        /// <remarks>
        /// <b>Uwaga:</b> Auto-discovery przez reflection nie jest AOT-safe.
        /// U¿yj Source Generator zamiast tego.
        /// </remarks>
        public bool AutoDiscoverHandlers { get; set; } = true;

        /// <summary>
        /// Lista assemblies, które maj¹ byæ przeskanowane w poszukiwaniu handlerów.
        /// </summary>
        public List<Assembly>? AssembliesToScan { get; set; }
    }
}
