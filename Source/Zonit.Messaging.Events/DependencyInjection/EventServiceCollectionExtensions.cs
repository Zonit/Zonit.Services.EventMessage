using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Messaging.Events.Hosting;

namespace Zonit.Messaging.Events;

/// <summary>
/// Extension methods for registering event handlers in DI.
/// </summary>
public static class EventServiceCollectionExtensions
{
    /// <summary>
    /// Registers event messaging services and all discovered event handlers.
    /// Source Generator automatically registers handlers when they exist.
    /// </summary>
    /// <remarks>
    /// This method is safe to call multiple times - uses TryAdd to prevent duplicates.
    /// Call this in your plugin's DI registration.
    /// </remarks>
    public static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        // Apply registrations from Source Generators (via ModuleInitializer)
        EventSegmentRegistry.ApplyRegistrations(services);

        // Register core services
        services.TryAddSingleton<IEventManager, EventManager>();
        services.TryAddSingleton<IEventProvider, EventProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, EventHandlerRegistrationHostedService>());

        return services;
    }

    /// <summary>
    /// Manually registers an event handler. AOT-safe version.
    /// Use this when you want explicit control over handler registration.
    /// </summary>
    /// <typeparam name="THandler">Handler type</typeparam>
    /// <typeparam name="TEvent">Event type</typeparam>
    public static IServiceCollection AddEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TEvent>(this IServiceCollection services)
        where THandler : class, IEventHandler<TEvent>
        where TEvent : notnull
    {
        services.AddEventHandlers();
        services.TryAddScoped<THandler>();
        // Multiple handlers per event are valid (pub/sub). Use TryAddEnumerable with concrete
        // impl type so GetServices<IEventHandler<TEvent>>() returns all registered handlers.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEventHandler<TEvent>, THandler>(static sp => sp.GetRequiredService<THandler>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<EventHandlerRegistration>(new EventHandlerRegistration<TEvent>()));
        return services;
    }

    /// <summary>
    /// Manually registers an event handler with options. AOT-safe version.
    /// </summary>
    public static IServiceCollection AddEvent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TEvent>(
        this IServiceCollection services,
        Action<EventSubscriptionOptions> configureOptions)
        where THandler : class, IEventHandler<TEvent>
        where TEvent : notnull
    {
        var options = new EventSubscriptionOptions();
        configureOptions(options);

        services.AddEventHandlers();
        services.TryAddScoped<THandler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEventHandler<TEvent>, THandler>(static sp => sp.GetRequiredService<THandler>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<EventHandlerRegistration>(new EventHandlerRegistration<TEvent>(options)));

        return services;
    }

    /// <summary>
    /// Registers event handler. Use AddEvent instead.
    /// </summary>
    [Obsolete("Use AddEvent<THandler, TEvent>() instead.")]
    public static IServiceCollection AddEventHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TEvent>(this IServiceCollection services)
        where THandler : class, IEventHandler<TEvent>
        where TEvent : notnull
        => services.AddEvent<THandler, TEvent>();

    /// <summary>
    /// Registers EventProvider. Use AddEventHandlers() instead.
    /// </summary>
    [Obsolete("Use AddEventHandlers() instead.")]
    public static IServiceCollection AddEventProvider(this IServiceCollection services)
        => services.AddEventHandlers();
}
