namespace Zonit.Messaging.Schedules;

/// <summary>
/// Internal infrastructure: adapts an <see cref="IScheduleHandler"/> (no-data) to the
/// typed <see cref="IScheduleHandler{TData}"/> pipeline by using a unique
/// <see cref="ScheduleMarker{THandler}"/> as the data type.
/// </summary>
/// <remarks>
/// Each <c>AddSchedule&lt;THandler&gt;</c> call registers its own adapter under
/// <c>IScheduleHandler&lt;ScheduleMarker&lt;THandler&gt;&gt;</c>, so scheduling THandler
/// never triggers other registered handlers.
/// </remarks>
/// <typeparam name="THandler">User handler type implementing <see cref="IScheduleHandler"/>.</typeparam>
internal sealed class ScheduleHandlerAdapter<THandler> : IScheduleHandler<ScheduleMarker<THandler>>
    where THandler : class, IScheduleHandler
{
    private readonly THandler _handler;

    public ScheduleHandlerAdapter(THandler handler)
    {
        _handler = handler;
    }

    public Task HandleAsync(ScheduleMarker<THandler> data, CancellationToken cancellationToken)
        => _handler.HandleAsync(cancellationToken);
}
