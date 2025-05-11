namespace Zonit.Services.EventMessage;

/// <summary>
/// Wspólny interfejs bazowy dla wszystkich handlerów (zarówno eventów jak i zadań)
/// </summary>
public interface IHandler
{
    /// <summary>
    /// Rejestruje handler w systemie
    /// </summary>
    void Subscribe(IServiceProvider serviceProvider);
}