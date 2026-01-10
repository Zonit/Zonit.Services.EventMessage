using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage.Base;

namespace Zonit.Services.EventMessage;

/// <summary>
/// [LEGACY] Interfejs bazowy dla handlerów zadañ.
/// </summary>
/// <remarks>
/// <para><b>Ten interfejs jest przestarza³y.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// U¿yj: <c>Zonit.Messaging.Tasks.ITaskHandler&lt;TTask&gt;</c>
/// </para>
/// </remarks>
[Obsolete("Use Zonit.Messaging.Tasks.ITaskHandler<TTask> instead.")]
public interface ITaskHandler : IHandler { }

/// <summary>
/// [LEGACY] Klasa bazowa dla handlerów zadañ.
/// </summary>
/// <remarks>
/// <para><b>Ta klasa jest przestarza³a.</b></para>
/// <para>
/// <b>Migracja:</b><br/>
/// Zamiast dziedziczenia po <c>TaskBase&lt;TModel&gt;</c>,<br/>
/// zaimplementuj interfejs <c>Zonit.Messaging.Tasks.ITaskHandler&lt;TTask&gt;</c><br/>
/// i zarejestruj przez:<br/>
/// <c>services.AddTaskHandler&lt;MyHandler, MyTask&gt;()</c>
/// </para>
/// </remarks>
/// <typeparam name="TModel">Typ danych zadania</typeparam>
[Obsolete("Inherit from Zonit.Messaging.Tasks.ITaskHandler<TTask> instead and register with AddTaskHandler<THandler, TTask>().")]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class TaskBase<TModel> : HandlerBase<TModel>, ITaskHandler where TModel : notnull
{
    /// <summary>
    /// Rejestruje handler w systemie zadañ.
    /// </summary>
    public override void Subscribe(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // U¿ywamy nowego Zonit.Messaging.Tasks.ITaskManager
        var taskManager = serviceProvider.GetRequiredService<Zonit.Messaging.Tasks.ITaskManager>();
        var logger = serviceProvider.GetRequiredService<ILogger<TaskBase<TModel>>>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        logger.LogInformation("[LEGACY] Registering task handler for '{ModelType}' with {WorkerCount} workers",
            typeof(TModel).Name, WorkerCount);

        // Subskrybuj przez nowe API
        taskManager.Subscribe<TModel>(async payload =>
        {
            var handler = CreateHandler(scopeFactory, logger);
            var legacyPayload = new PayloadModel<TModel>
            {
                Data = payload.Data,
                CancellationToken = payload.CancellationToken
            };
            await handler(legacyPayload);
        }, new Zonit.Messaging.Tasks.TaskSubscriptionOptions
        {
            WorkerCount = WorkerCount,
            Timeout = ExecutionTimeout
        });
    }
}
