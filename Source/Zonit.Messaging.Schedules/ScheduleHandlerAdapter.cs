namespace Zonit.Messaging.Schedules;

/// <summary>
/// Adapter that wraps an <see cref="IScheduleHandler"/> as <see cref="IScheduleHandler{Unit}"/>.
/// This is an internal infrastructure class and should not be used directly.
/// </summary>
/// <typeparam name="THandler">The handler type implementing IScheduleHandler.</typeparam>
internal abstract class ScheduleHandlerAdapterBase<THandler> : IScheduleHandler<Unit>
    where THandler : class, IScheduleHandler
{
    private readonly THandler _handler;

    protected ScheduleHandlerAdapterBase(THandler handler)
    {
        _handler = handler;
    }

    public Task HandleAsync(Unit data, CancellationToken cancellationToken)
    {
        return _handler.HandleAsync(cancellationToken);
    }
}

/// <summary>
/// Concrete adapter - abstract base prevents Source Generator from detecting it.
/// </summary>
internal sealed class ScheduleHandlerAdapter<THandler> : ScheduleHandlerAdapterBase<THandler>
    where THandler : class, IScheduleHandler
{
    public ScheduleHandlerAdapter(THandler handler) : base(handler) { }
}
