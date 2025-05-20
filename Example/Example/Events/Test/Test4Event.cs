using Example.Models;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage;

namespace Example.Events.Test;
internal class Test4Event(ILogger<Test4Event> _logger) : EventBase<Test4Model>
{
    protected override async Task HandleAsync(Test4Model payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        _logger.LogInformation("[TestEvent] Number: {number} Title: {title}", 4, payload);
    }
}