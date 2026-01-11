using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Messaging.Commands;

/// <summary>
/// Registry for command handler segment registrations.
/// Source Generators use ModuleInitializer to register their segments here.
/// When AddCommandHandlers() is called, all registered segments are applied.
/// </summary>
public static class CommandSegmentRegistry
{
    private static readonly List<Action<IServiceCollection>> _registrations = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Registers a handler registration action.
    /// Called by Source Generator's ModuleInitializer.
    /// </summary>
    public static void Register(Action<IServiceCollection> registration)
    {
        lock (_lock)
        {
            _registrations.Add(registration);
        }
    }

    /// <summary>
    /// Applies all registered handler registrations to the service collection.
    /// Called internally by AddCommandHandlers().
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
}

/// <summary>
/// Marker attribute for generated command handler registration sources.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandHandlerRegistrationSourceAttribute : Attribute
{
}
