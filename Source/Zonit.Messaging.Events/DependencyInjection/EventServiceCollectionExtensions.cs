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
    /// Dodaje serwisy eventów do kontenera DI.
    /// U¿yj AddEventHandlers() z Source Generator dla pe³nego AOT support.
    /// </summary>
    public static IServiceCollection AddEventProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<IEventManager, EventManager>();
        services.TryAddSingleton<IEventProvider, EventProvider>();
        services.AddHostedService<EventHandlerRegistrationHostedService>();
        return services;
    }

    /// <summary>
    /// Rejestruje handler eventów rêcznie (bez Source Generator).
    /// </summary>
    /// <typeparam name="THandler">Typ handlera</typeparam>
    /// <typeparam name="TEvent">Typ eventu</typeparam>
    public static IServiceCollection AddEventHandler<THandler, TEvent>(this IServiceCollection services)
        where THandler : class, IEventHandler<TEvent>
        where TEvent : notnull
    {
        services.AddScoped<THandler>();
        services.AddScoped<IEventHandler<TEvent>>(sp => sp.GetRequiredService<THandler>());
        services.AddSingleton<EventHandlerRegistration>(new EventHandlerRegistration<TEvent>());
        return services;
    }

    /// <summary>
    /// Rejestruje handler eventów z okreœlonymi opcjami.
    /// </summary>
    public static IServiceCollection AddEventHandler<THandler, TEvent>(
        this IServiceCollection services,
        Action<EventSubscriptionOptions> configureOptions)
        where THandler : class, IEventHandler<TEvent>
        where TEvent : notnull
    {
        var options = new EventSubscriptionOptions();
        configureOptions(options);

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
