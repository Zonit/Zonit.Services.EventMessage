using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Extension methods for registering schedule services.
/// </summary>
public static class ScheduleServiceCollectionExtensions
{
    /// <summary>
    /// Adds schedule services to the service collection.
    /// Call this once to register the core scheduling infrastructure.
    /// Also applies any handlers registered via Source Generators.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScheduleServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ScheduleManager>();
        services.TryAddSingleton<IScheduleProvider>(sp => sp.GetRequiredService<ScheduleManager>());

        // Apply handlers registered by Source Generators
        ScheduleSegmentRegistry.ApplyRegistrations(services);

        return services;
    }

    /// <summary>
    /// Registers a schedule handler for the specified data type.
    /// For AOT compatibility, prefer using the Source Generator which auto-discovers handlers.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TData">The data type the handler processes.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScheduleHandler<THandler, TData>(this IServiceCollection services)
        where THandler : class, IScheduleHandler<TData>
        where TData : notnull
    {
        services.AddScheduleServices();
        services.TryAddScoped<THandler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IScheduleHandler<TData>>(sp => sp.GetRequiredService<THandler>()));
        return services;
    }

    /// <summary>
    /// Registers a schedule handler for the specified data type with a factory.
    /// </summary>
    /// <typeparam name="TData">The data type the handler processes.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="implementationFactory">The factory to create the handler.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScheduleHandler<TData>(
        this IServiceCollection services,
        Func<IServiceProvider, IScheduleHandler<TData>> implementationFactory)
        where TData : notnull
    {
        services.AddScheduleServices();
        services.TryAddEnumerable(ServiceDescriptor.Scoped(implementationFactory));
        return services;
    }
}
