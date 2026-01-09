using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Zonit.Messaging.Commands;

/// <summary>
/// Domyœlna implementacja ICommandProvider.
/// U¿ywa DI do znajdowania odpowiednich handlerów.
/// Dla AOT/trimming u¿yj GeneratedCommandProvider z Source Generatora (AddCommandHandlers()).
/// </summary>
[RequiresDynamicCode("This provider uses reflection. For AOT/trimming, use AddCommandHandlers() which provides GeneratedCommandProvider.")]
public sealed class CommandProvider : ICommandProvider
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly ConcurrentDictionary<(Type, Type), Type> _wrapperTypeCache = new();

    public CommandProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResponse?> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : notnull
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();

        // Cache typu wrappera - unikamy kosztownego MakeGenericType przy ka¿dym wywo³aniu
        var wrapperType = _wrapperTypeCache.GetOrAdd(
            (requestType, typeof(TResponse)),
            static key => typeof(RequestHandlerWrapper<,>).MakeGenericType(key.Item1, key.Item2)
        );

        var wrapper = _serviceProvider.GetService(wrapperType) as IRequestHandlerWrapper<TResponse>;

        if (wrapper is null)
        {
            throw new InvalidOperationException(
                $"No handler registered for request type '{requestType.FullName}'. " +
                $"Expected response type: '{typeof(TResponse).FullName}'. " +
                $"Ensure handler is registered using services.AddCommand<THandler>()."
            );
        }

        return wrapper.HandleAsync(request, cancellationToken);
    }
}
