using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zonit.Messaging.Commands;

/// <summary>
/// Extension methods for registering command handlers in DI.
/// </summary>
public static class CommandServiceCollectionExtensions
{
    /// <summary>
    /// Registers command messaging services.
    /// Source Generator automatically registers handler segments when handlers exist.
    /// </summary>
    /// <remarks>
    /// This method is safe to call multiple times - uses TryAdd to prevent duplicates.
    /// Call this in your plugin's DI registration.
    /// </remarks>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        // Apply registrations from Source Generators (via ModuleInitializer)
        CommandSegmentRegistry.ApplyRegistrations(services);
        
        // Register composite provider that delegates to segments
        services.TryAddScoped<ICommandProvider, CompositeCommandProvider>();
        
        return services;
    }

    /// <summary>
    /// Manually registers a command handler. AOT-safe version.
    /// Use this when you want explicit control over handler registration.
    /// </summary>
    /// <typeparam name="THandler">Handler type implementing IRequestHandler&lt;TRequest, TResponse&gt;</typeparam>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    public static IServiceCollection AddCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TRequest, TResponse>(this IServiceCollection services)
        where THandler : class, IRequestHandler<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : notnull
    {
        services.AddCommandHandlers();
        services.TryAddScoped<THandler>();
        services.TryAddScoped<IRequestHandler<TRequest, TResponse>>(sp => sp.GetRequiredService<THandler>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICommandProviderSegment, SingleCommandSegment<TRequest, TResponse>>());
        
        return services;
    }

    /// <summary>
    /// Registers CommandProvider. Use AddCommandHandlers() instead.
    /// </summary>
    [Obsolete("Use AddCommandHandlers() instead. This method will be removed in future versions.")]
    public static IServiceCollection AddCommandProvider(this IServiceCollection services)
        => services.AddCommandHandlers();
}

/// <summary>
/// A segment that handles a single command type. Used for manual registration.
/// </summary>
internal sealed class SingleCommandSegment<TRequest, TResponse> : ICommandProviderSegment
    where TRequest : IRequest<TResponse>
    where TResponse : notnull
{
    public bool TryHandle<TResult>(
        IRequest<TResult> request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        out Task<TResult?> result)
        where TResult : notnull
    {
        if (request is TRequest typedRequest)
        {
            var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            // IRequest is invariant, so TResponse == TResult at runtime here: reinterpret the
            // handler's Task<TResponse> as Task<TResult?> (identity, no boxing, no extra Task).
            var task = handler.HandleAsync(typedRequest, cancellationToken);
            result = Unsafe.As<Task<TResult?>>(task);
            return true;
        }

        result = null!;
        return false;
    }
}
