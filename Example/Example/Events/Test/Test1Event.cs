using Example.Models;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage;

namespace Example.Events.Test;
internal class Test1Event(ILogger<Test1Event> _logger) : EventBase<Test1Model>
{
    protected override async Task HandleAsync(Test1Model payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        _logger.LogInformation("[TestEvent] Number: {number} Title: {title}", 1, payload);
    }
}