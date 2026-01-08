using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zonit.Messaging.Commands;

/// <summary>
/// Extension methods dla rejestracji command handlerów w DI.
/// </summary>
public static class CommandServiceCollectionExtensions
{
    /// <summary>
    /// Dodaje CommandProvider do kontenera DI.
    /// Wywo³aj tê metodê raz podczas konfiguracji serwisów.
    /// </summary>
    public static IServiceCollection AddCommandProvider(this IServiceCollection services)
    {
        services.TryAddScoped<ICommandProvider, CommandProvider>();
        return services;
    }

    /// <summary>
    /// Rejestruje handler w kontenerze DI.
    /// Handler musi implementowaæ IRequestHandler&lt;TRequest, TResponse&gt;.
    /// Uwaga: Ta metoda u¿ywa reflection i nie jest AOT-safe.
    /// Dla AOT/trimming u¿yj AddCommandHandlers() z Source Generatora.
    /// </summary>
    /// <typeparam name="THandler">Typ handlera implementuj¹cy IRequestHandler</typeparam>
    [RequiresDynamicCode("This method uses reflection to register handlers. For AOT/trimming, use AddCommandHandlers() from the source generator.")]
    [RequiresUnreferencedCode("This method uses reflection to discover handler interfaces. For AOT/trimming, use AddCommandHandlers() from the source generator.")]
    public static IServiceCollection AddCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where THandler : class
    {
        var handlerType = typeof(THandler);

        var handlerInterface = handlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

        if (handlerInterface is null)
        {
            throw new InvalidOperationException(
                $"{handlerType.Name} must implement IRequestHandler<TRequest, TResponse>. " +
                $"Ensure the handler class implements the interface correctly."
            );
        }

        var requestType = handlerInterface.GetGenericArguments()[0];
        var responseType = handlerInterface.GetGenericArguments()[1];

        // Rejestruj handler
        services.AddScoped(handlerInterface, handlerType);

        // Rejestruj wrapper
        var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, responseType);
        services.AddScoped(wrapperType, sp =>
        {
            var handler = sp.GetRequiredService(handlerInterface);
            return Activator.CreateInstance(wrapperType, handler)!;
        });

        return services;
    }

    /// <summary>
    /// Rejestruje handler z okreœlonym czasem ¿ycia.
    /// Uwaga: Ta metoda u¿ywa reflection i nie jest AOT-safe.
    /// </summary>
    [RequiresDynamicCode("This method uses reflection. For AOT/trimming, use AddCommandHandlers() from the source generator.")]
    [RequiresUnreferencedCode("This method uses reflection. For AOT/trimming, use AddCommandHandlers() from the source generator.")]
    public static IServiceCollection AddCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services, ServiceLifetime lifetime)
        where THandler : class
    {
        var handlerType = typeof(THandler);

        var handlerInterface = handlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

        if (handlerInterface is null)
        {
            throw new InvalidOperationException(
                $"{handlerType.Name} must implement IRequestHandler<TRequest, TResponse>."
            );
        }

        var requestType = handlerInterface.GetGenericArguments()[0];
        var responseType = handlerInterface.GetGenericArguments()[1];

        // Rejestruj handler z okreœlonym lifetime
        services.Add(new ServiceDescriptor(handlerInterface, handlerType, lifetime));

        // Rejestruj wrapper z tym samym lifetime
        var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(requestType, responseType);
        services.Add(new ServiceDescriptor(wrapperType, sp =>
        {
            var handler = sp.GetRequiredService(handlerInterface);
            return Activator.CreateInstance(wrapperType, handler)!;
        }, lifetime));

        return services;
    }
}
