using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Messaging.Events.Hosting;

namespace Zonit.Messaging.Events;

/// <summary>
/// Extension methods dla rejestracji serwisów eventów w DI.
/// </summary>
public static class EventServiceCollectionExtensions
{
    /// <summary>
    /// Registers event messaging services and all discovered event handlers.
    /// Use this method in your plugin's DI registration - it works with or without handlers.
    /// Source Generator automatically adds handler registrations when handlers exist.
    /// </summary>
    /// <remarks>
    /// This method is safe to call multiple times - uses TryAdd to prevent duplicates.
    /// </remarks>
    public static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventManager, EventManager>();
        services.TryAddSingleton<IEventProvider, EventProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, EventHandlerRegistrationHostedService>());
        
        // Apply all registrations from Source Generators
        EventHandlerRegistry.ApplyRegistrations(services);
        
        return services;
    }

    /// <summary>
    /// Dodaje serwisy eventów do kontenera DI.
    /// U¿yj AddEventHandlers() zamiast tej metody.
    /// </summary>
    [Obsolete("Use AddEventHandlers() instead. This method will be removed in future versions.")]
    public static IServiceCollection AddEventProvider(this IServiceCollection services)
    {
        return services.AddEventHandlers();
    }

    /// <summary>
    /// Rejestruje handler eventów rêcznie (bez Source Generator).
    /// </summary>
    /// <typeparam name="THandler">Typ handlera</typeparam>
    /// <typeparam name="TEvent">Typ eventu</typeparam>
    public static IServiceCollection AddEventHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TEvent>(this IServiceCollection services)
        where THandler : class, IEventHandler<TEvent>
        where TEvent : notnull
    {
        services.AddEventHandlers(); // Ensure base services are registered
        services.AddScoped<THandler>();
        services.AddScoped<IEventHandler<TEvent>>(sp => sp.GetRequiredService<THandler>());
        services.AddSingleton<EventHandlerRegistration>(new EventHandlerRegistration<TEvent>());
        return services;
    }

    /// <summary>
    /// Rejestruje handler eventów z okreœlonymi opcjami.
    /// </summary>
    public static IServiceCollection AddEventHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TEvent>(
        this IServiceCollection services,
        Action<EventSubscriptionOptions> configureOptions)
        where THandler : class, IEventHandler<TEvent>
        where TEvent : notnull
    {
        var options = new EventSubscriptionOptions();
        configureOptions(options);

        services.AddEventHandlers(); // Ensure base services are registered
        services.AddScoped<THandler>();
        services.AddScoped<IEventHandler<TEvent>>(sp => sp.GetRequiredService<THandler>());
        services.AddSingleton<EventHandlerRegistration>(new EventHandlerRegistration<TEvent>(options));

        return services;
    }
}

/// <summary>
/// Opcje dla konkretnego handlera eventów.
/// </summary>
public class EventHandlerOptions<TEvent> where TEvent : notnull
{
    public int WorkerCount { get; set; } = 10;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool ContinueOnError { get; set; } = true;
}
