using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Zonit.Extensions;

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
        services.TryAddSingleton<IScheduleProvider>(static sp => sp.GetRequiredService<ScheduleManager>());

        // Register the hosted service once (TryAddEnumerable deduplicates by implementation type).
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, ScheduleHostedService>());

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
    /// // Run once immediately, then every Friday at 22:00 UTC
    /// services.AddSchedule&lt;SyncHandler&gt;(Schedule.Now(), Schedule.EveryWeek(DayOfWeek.Friday, 22));
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
    public static IServiceCollection AddSchedule<THandler>(
        this IServiceCollection services,
        Action<ScheduleOptions> configure)
        where THandler : class, IScheduleHandler
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.AddScheduleServices();

        // Register user handler. Use the two-arg generic overload so the factory's delegate type
        // is Func<IServiceProvider, THandler>, keeping the concrete implementation type visible
        // to the DI container (required by TryAddEnumerable).
        services.TryAddScoped<THandler>(static sp => ActivatorUtilities.CreateInstance<THandler>(sp));

        // Each handler gets its own unique ScheduleMarker<THandler> data type so schedules do not
        // cross-trigger other handlers (earlier design used shared Unit which caused this bug).
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IScheduleHandler<ScheduleMarker<THandler>>, ScheduleHandlerAdapter<THandler>>(
                static sp => new ScheduleHandlerAdapter<THandler>(sp.GetRequiredService<THandler>())));

        var options = new ScheduleOptions();
        configure(options);

        if (options.Schedules.Length == 0)
            throw new ArgumentException("At least one schedule is required in options.");

        services.AddSingleton<ScheduleRegistration>(new ScheduleRegistration<THandler>
        {
            Schedules = options.Schedules,
            Name = options.Name,
            ExecuteOnStartup = options.ExecuteOnStartup,
            Timeout = options.Timeout,
            MaxRetries = options.MaxRetries,
            RetryDelay = options.RetryDelay,
            StopOnMaxRetries = options.StopOnMaxRetries,
            Description = options.Description,
            TimeZone = options.TimeZone
        });

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
        services.TryAddScoped<THandler>(static sp => ActivatorUtilities.CreateInstance<THandler>(sp));

        // Two-arg generic overload → factory runtime type is Func<IServiceProvider, THandler>,
        // so GetImplementationType() returns THandler (distinct from IScheduleHandler<TData>).
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IScheduleHandler<TData>, THandler>(
                static sp => sp.GetRequiredService<THandler>()));
        return services;
    }

    /// <summary>
    /// Registers a schedule handler for the specified data type with a typed factory.
    /// The factory must return the concrete handler type (not the interface) so DI can
    /// deduplicate entries correctly.
    /// </summary>
    /// <typeparam name="THandler">The concrete handler type.</typeparam>
    /// <typeparam name="TData">The data type the handler processes.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="implementationFactory">The factory to create the handler.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScheduleHandler<THandler, TData>(
        this IServiceCollection services,
        Func<IServiceProvider, THandler> implementationFactory)
        where THandler : class, IScheduleHandler<TData>
        where TData : notnull
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);

        services.AddScheduleServices();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IScheduleHandler<TData>, THandler>(implementationFactory));
        return services;
    }
}
