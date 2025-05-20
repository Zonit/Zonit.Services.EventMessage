using Example.Models;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage;

namespace Example.Events.Test;
internal class Test5Event(ILogger<Test5Event> _logger) : EventBase<Test5Model>
{
    protected override async Task HandleAsync(Test5Model payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        _logger.LogInformation("[TestEvent] Number: {number} Title: {title}", 5, payload);
    }
}