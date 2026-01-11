using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Messaging.Tasks.Hosting;

namespace Zonit.Messaging.Tasks;

/// <summary>
/// Extension methods for registering task handlers in DI.
/// </summary>
public static class TaskServiceCollectionExtensions
{
    /// <summary>
    /// Registers task messaging services and all discovered task handlers.
    /// Source Generator automatically registers handlers when they exist.
    /// </summary>
    /// <remarks>
    /// This method is safe to call multiple times - uses TryAdd to prevent duplicates.
    /// Call this in your plugin's DI registration.
    /// </remarks>
    public static IServiceCollection AddTaskHandlers(this IServiceCollection services)
    {
        // Apply registrations from Source Generators (via ModuleInitializer)
        TaskSegmentRegistry.ApplyRegistrations(services);
        
        // Register core services
        services.TryAddSingleton<ITaskManager, TaskManager>();
        services.TryAddSingleton<ITaskProvider, TaskProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, TaskHandlerRegistrationHostedService>());
        
        return services;
    }

    /// <summary>
    /// Manually registers a task handler. AOT-safe version.
    /// Use this when you want explicit control over handler registration.
    /// </summary>
    /// <typeparam name="THandler">Handler type</typeparam>
    /// <typeparam name="TTask">Task type</typeparam>
    public static IServiceCollection AddTask<THandler, TTask>(this IServiceCollection services)
        where THandler : class, ITaskHandler<TTask>
        where TTask : notnull
    {
        services.AddTaskHandlers();
        services.TryAddScoped<THandler>();
        services.TryAddScoped<ITaskHandler<TTask>>(sp => sp.GetRequiredService<THandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<TaskHandlerRegistration>(new TaskHandlerRegistration<TTask>()));
        return services;
    }

    /// <summary>
    /// Manually registers a task handler with options. AOT-safe version.
    /// </summary>
    public static IServiceCollection AddTask<THandler, TTask>(
        this IServiceCollection services,
        Action<TaskSubscriptionOptions> configureOptions)
        where THandler : class, ITaskHandler<TTask>
        where TTask : notnull
    {
        var options = new TaskSubscriptionOptions();
        configureOptions(options);

        services.AddTaskHandlers();
        services.TryAddScoped<THandler>();
        services.TryAddScoped<ITaskHandler<TTask>>(sp => sp.GetRequiredService<THandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<TaskHandlerRegistration>(new TaskHandlerRegistration<TTask>(options)));

        return services;
    }

    /// <summary>
    /// Registers task handler. Use AddTask instead.
    /// </summary>
    [Obsolete("Use AddTask<THandler, TTask>() instead.")]
    public static IServiceCollection AddTaskHandler<THandler, TTask>(this IServiceCollection services)
        where THandler : class, ITaskHandler<TTask>
        where TTask : notnull
        => services.AddTask<THandler, TTask>();

    /// <summary>
    /// Registers TaskProvider. Use AddTaskHandlers() instead.
    /// </summary>
    [Obsolete("Use AddTaskHandlers() instead.")]
    public static IServiceCollection AddTaskProvider(this IServiceCollection services)
        => services.AddTaskHandlers();
}
