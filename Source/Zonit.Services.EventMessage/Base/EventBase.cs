using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage.Base;

namespace Zonit.Services.EventMessage;

/// <summary>
/// [LEGACY] Interfejs bazowy dla handlerów eventów.
/// </summary>
/// <remarks>
/// <para><b>Ten interfejs jest przestarza³y.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// U¿yj: <c>Zonit.Messaging.Events.IEventHandler&lt;TEvent&gt;</c>
/// </para>
/// </remarks>
[Obsolete("Use Zonit.Messaging.Events.IEventHandler<TEvent> instead.")]
public interface IEventHandler : IHandler { }

/// <summary>
/// [LEGACY] Klasa bazowa dla handlerów zdarzeñ, automatyzuj¹ca proces rejestracji.
/// </summary>
/// <remarks>
/// <para><b>Ta klasa jest przestarza³a.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// Zamiast dziedziczenia po <c>EventBase&lt;TModel&gt;</c>,<br/>
/// zaimplementuj interfejs <c>Zonit.Messaging.Events.IEventHandler&lt;TEvent&gt;</c><br/>
/// i zarejestruj przez:<br/>
/// <c>services.AddEventHandler&lt;MyHandler, MyEvent&gt;()</c>
/// </para>
/// </remarks>
[Obsolete("Inherit from Zonit.Messaging.Events.IEventHandler<TEvent> instead and register with AddEventHandler<THandler, TEvent>().")]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class EventBase<TModel> : HandlerBase<TModel>, IEventHandler
{
    /// <summary>
    /// Nazwa zdarzenia, do którego subskrybuje handler.
    /// </summary>
    protected virtual string EventName => typeof(TModel).FullName ?? typeof(TModel).Name;

    /// <summary>
    /// Domyœlny czas wykonania handlera zdarzeñ.
    /// </summary>
    protected override TimeSpan ExecutionTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Rejestruje handler w systemie zdarzeñ.
    /// </summary>
    public override void Subscribe(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        if (string.IsNullOrEmpty(EventName))
        {
            throw new InvalidOperationException($"Event name cannot be null or empty in {GetType().Name}");
        }

        // U¿ywamy nowego Zonit.Messaging.Events.IEventManager
        var eventManager = serviceProvider.GetRequiredService<Zonit.Messaging.Events.IEventManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<EventBase<TModel>>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        logger.LogInformation("[LEGACY] Registering event handler for '{EventName}' with {WorkerCount} workers",
            EventName, WorkerCount);

        // Subskrybuj przez nowe API
        eventManager.Subscribe<TModel>(async (data, cancellationToken) =>
        {
            var handler = CreateHandler(scopeFactory, logger);
            var legacyPayload = new PayloadModel<TModel>
            {
                Data = data,
                CancellationToken = cancellationToken
            };
            await handler(legacyPayload);
        }, new Zonit.Messaging.Events.EventSubscriptionOptions
        {
            WorkerCount = WorkerCount,
            Timeout = ExecutionTimeout
        });
    }
}

/// <summary>
/// [LEGACY] Klasa bazowa dla handlerów zdarzeñ z nietypowanymi danymi.
/// </summary>
[Obsolete("Use Zonit.Messaging.Events.IEventHandler<TEvent> instead.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class EventBase : EventBase<object>
{
    /// <summary>
    /// Nazwa zdarzenia, do którego subskrybuje handler.
    /// </summary>
    protected abstract override string EventName { get; }

    /// <summary>
    /// W³aœciwa metoda obs³ugi zdarzenia.
    /// </summary>
    protected abstract override Task HandleAsync(object data, CancellationToken cancellationToken);
}
