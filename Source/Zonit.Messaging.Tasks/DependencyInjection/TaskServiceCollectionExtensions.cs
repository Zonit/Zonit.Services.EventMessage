using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zonit.Messaging.Tasks.Hosting;

namespace Zonit.Messaging.Tasks;

/// <summary>
/// Extension methods dla rejestracji serwisów zadañ w DI.
/// </summary>
public static class TaskServiceCollectionExtensions
{
    /// <summary>
    /// Registers task messaging services and all discovered task handlers.
    /// Use this method in your plugin's DI registration - it works with or without handlers.
    /// Source Generator automatically adds handler registrations when handlers exist.
    /// </summary>
    /// <remarks>
    /// This method is safe to call multiple times - uses TryAdd to prevent duplicates.
    /// </remarks>
    public static IServiceCollection AddTaskHandlers(this IServiceCollection services)
    {
        services.TryAddSingleton<ITaskManager, TaskManager>();
        services.TryAddSingleton<ITaskProvider, TaskProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, TaskHandlerRegistrationHostedService>());
        
        // Apply all registrations from Source Generators
        TaskHandlerRegistry.ApplyRegistrations(services);
        
        return services;
    }

    /// <summary>
    /// Dodaje serwisy zadañ do kontenera DI.
    /// U¿yj AddTaskHandlers() zamiast tej metody.
    /// </summary>
    [Obsolete("Use AddTaskHandlers() instead. This method will be removed in future versions.")]
    public static IServiceCollection AddTaskProvider(this IServiceCollection services)
    {
        return services.AddTaskHandlers();
    }

    /// <summary>
    /// Rejestruje handler zadañ rêcznie (bez Source Generator).
    /// </summary>
    /// <typeparam name="THandler">Typ handlera</typeparam>
    /// <typeparam name="TTask">Typ zadania</typeparam>
    public static IServiceCollection AddTaskHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TTask>(this IServiceCollection services)
        where THandler : class, ITaskHandler<TTask>
        where TTask : notnull
    {
        services.AddTaskHandlers(); // Ensure base services are registered
        services.AddScoped<THandler>();
        services.AddScoped<ITaskHandler<TTask>>(sp => sp.GetRequiredService<THandler>());
        services.AddSingleton<TaskHandlerRegistration>(new TaskHandlerRegistration<TTask>());
        return services;
    }

    /// <summary>
    /// Rejestruje handler zadañ z opcjami.
    /// </summary>
    public static IServiceCollection AddTaskHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TTask>(
        this IServiceCollection services,
        Action<TaskSubscriptionOptions> configureOptions)
        where THandler : class, ITaskHandler<TTask>
        where TTask : notnull
    {
        var options = new TaskSubscriptionOptions();
        configureOptions(options);

        services.AddTaskHandlers(); // Ensure base services are registered
        services.AddScoped<THandler>();
        services.AddScoped<ITaskHandler<TTask>>(sp => sp.GetRequiredService<THandler>());
        services.AddSingleton<TaskHandlerRegistration>(new TaskHandlerRegistration<TTask>(options));

        return services;
    }
}
