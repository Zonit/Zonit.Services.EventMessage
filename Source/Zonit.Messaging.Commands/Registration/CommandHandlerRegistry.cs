using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Messaging.Commands;

/// <summary>
/// Registry for command handler registrations.
/// Source Generators use this to register their handlers automatically.
/// </summary>
public static class CommandHandlerRegistry
{
    private static readonly List<Action<IServiceCollection>> _registrations = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Registers a handler registration action.
    /// Called by Source Generator's ModuleInitializer.
    /// </summary>
    /// <param name="registration">Action that registers handlers in DI</param>
    public static void Register(Action<IServiceCollection> registration)
    {
        lock (_lock)
        {
            _registrations.Add(registration);
        }
    }

    /// <summary>
    /// Applies all registered handler registrations to the service collection.
    /// Called by AddCommandHandlers().
    /// </summary>
    internal static void ApplyRegistrations(IServiceCollection services)
    {
        lock (_lock)
        {
            foreach (var registration in _registrations)
            {
                registration(services);
            }
        }
    }

    /// <summary>
    /// Gets the number of registered sources (for diagnostics).
    /// </summary>
    public static int RegisteredSourceCount
    {
        get
        {
            lock (_lock)
            {
                return _registrations.Count;
            }
        }
    }
}

/// <summary>
/// Marker attribute for generated command handler registration sources.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandHandlerRegistrationSourceAttribute : Attribute
{
}
