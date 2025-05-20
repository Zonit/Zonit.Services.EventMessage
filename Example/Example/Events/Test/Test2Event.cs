using Example.Models;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage;

namespace Example.Events.Test;
internal class Test2Event(ILogger<Test2Event> _logger) : EventBase<Test2Model>
{
    protected override async Task HandleAsync(Test2Model payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        _logger.LogInformation("[TestEvent] Number: {number} Title: {title}", 2, payload);
    }
}