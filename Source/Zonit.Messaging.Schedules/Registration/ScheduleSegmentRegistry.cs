using Microsoft.Extensions.DependencyInjection;

namespace Zonit.Messaging.Schedules;

/// <summary>
/// Registry for schedule handler segment registrations.
/// Source Generators use ModuleInitializer to register their segments here.
/// </summary>
public static class ScheduleSegmentRegistry
{
    private static readonly List<Action<IServiceCollection>> _registrations = [];
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
