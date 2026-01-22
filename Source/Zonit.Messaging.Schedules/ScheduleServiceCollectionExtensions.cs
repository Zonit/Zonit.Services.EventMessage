using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Extensions;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Extension methods for registering schedule services.
/// </summary>
public static class ScheduleServiceCollectionExtensions
{
    private static bool _hostedServiceRegistered;

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
    /// Registers a simple schedule handler that runs on a schedule (like BackgroundService).
    /// The handler will be started automatically when the application starts.
    /// </summary>
    /// <typeparam name="THandler">Handler type implementing <see cref="IScheduleHandler"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="schedules">One or more schedule rules.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // Run cleanup every 10 minutes
    /// services.AddSchedule&lt;CleanupHandler&gt;(Schedule.EveryMinutes(10));
    /// 
    /// // Run daily at 3:00 AM
    /// services.AddSchedule&lt;ReportHandler&gt;(Schedule.EveryDay(3, 0));
    /// 
    /// // Run at multiple times
    /// services.AddSchedule&lt;SyncHandler&gt;(Schedule.EveryDay(8, 0), Schedule.EveryDay(18, 0));
    /// </code>
    /// </example>
    public static IServiceCollection AddSchedule<THandler>(
        this IServiceCollection services,
        params Schedule[] schedules)
        where THandler : class, IScheduleHandler
    {
        return services.AddSchedule<THandler>(options =>
        {
            options.Schedules = schedules;
        });
    }

    /// <summary>
    /// Registers a simple schedule handler with full configuration options.
    /// The handler will be started automatically when the application starts.
    /// </summary>
    /// <typeparam name="THandler">Handler type implementing <see cref="IScheduleHandler"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for schedule options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSchedule&lt;CleanupHandler&gt;(options =>
    /// {
    ///     options.Name = "Cleanup Task";
    ///     options.Schedules = [Schedule.EveryMinutes(30)];
    ///     options.ExecuteOnStartup = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSchedule<THandler>(
        this IServiceCollection services,
        Action<ScheduleOptions> configure)
        where THandler : class, IScheduleHandler
    {
        services.AddScheduleServices();

        // Register handler using factory with ActivatorUtilities for DI support
        services.TryAddScoped<THandler>(sp => ActivatorUtilities.CreateInstance<THandler>(sp));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IScheduleHandler<Unit>>(
            sp => new ScheduleHandlerAdapter<THandler>(sp.GetRequiredService<THandler>())));

        // Create registration for hosted service
        var options = new ScheduleOptions();
        configure(options);

        if (options.Schedules.Length == 0)
            throw new ArgumentException("At least one schedule is required in options.");

        services.AddSingleton(new ScheduleRegistration
        {
            HandlerType = typeof(THandler),
            Schedules = options.Schedules,
            Name = options.Name ?? typeof(THandler).Name,
            ExecuteOnStartup = options.ExecuteOnStartup
        });

        // Register hosted service once
        if (!_hostedServiceRegistered)
        {
            services.AddHostedService<ScheduleHostedService>();
            _hostedServiceRegistered = true;
        }

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
        // Using ActivatorUtilities for AOT compatibility with DI support
        services.TryAddScoped<THandler>(sp => ActivatorUtilities.CreateInstance<THandler>(sp));
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
