using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Zonit.Services.EventMessage;
using Zonit.Services.EventMessage.Services;

namespace Zonit.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventMessageService(this IServiceCollection services)
    {
        services.AddSingleton<IEventProvider, EventBusService>();
        services.AddHostedService<EventHandlersHostedService>();
        services.AddEventMessageHandlers();

        return services;
    }

    public static IServiceCollection AddEventMessageHandlers(this IServiceCollection services)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        var handlerTypes = assemblies
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null)!;
                }
            })
            .Where(t => typeof(IEventHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in handlerTypes)
            if (!services.Any(s => s.ServiceType == typeof(IEventHandler) && s.ImplementationType == type) && type is not null)
                services.AddTransient(typeof(IEventHandler), type);
            
        return services;
    }

}