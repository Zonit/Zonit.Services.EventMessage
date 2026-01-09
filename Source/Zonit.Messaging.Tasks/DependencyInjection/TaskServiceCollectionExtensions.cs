using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zonit.Messaging.Tasks;

/// <summary>
/// Extension methods dla rejestracji serwisów zadañ w DI.
/// </summary>
public static class TaskServiceCollectionExtensions
{
    /// <summary>
    /// Dodaje serwisy zadañ do kontenera DI.
    /// U¿yj AddTaskHandlers() z Source Generator dla pe³nego AOT support.
    /// </summary>
    public static IServiceCollection AddTaskProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<ITaskManager, TaskManager>();
        services.TryAddSingleton<ITaskProvider, TaskProvider>();
        return services;
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
        services.AddScoped<THandler>();
        services.AddScoped<ITaskHandler<TTask>>(sp => sp.GetRequiredService<THandler>());
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

        services.AddScoped<THandler>();
        services.AddScoped<ITaskHandler<TTask>>(sp => sp.GetRequiredService<THandler>());
        
        services.Configure<TaskHandlerOptions<TTask>>(opt =>
        {
            opt.WorkerCount = options.WorkerCount;
            opt.Timeout = options.Timeout;
            opt.ContinueOnError = options.ContinueOnError;
            opt.MaxRetries = options.MaxRetries;
            opt.RetryDelay = options.RetryDelay;
        });

        return services;
    }
}

/// <summary>
/// Opcje dla konkretnego handlera zadañ.
/// </summary>
public class TaskHandlerOptions<TTask> where TTask : notnull
{
    public int WorkerCount { get; set; } = 10;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool ContinueOnError { get; set; } = true;
    public int MaxRetries { get; set; } = 0;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
