using Example.Models;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage;

namespace Example.Events.Test;
internal class Test3Event(ILogger<Test3Event> _logger) : EventBase<Test3Model>
{
    protected override async Task HandleAsync(Test3Model payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        _logger.LogInformation("[TestEvent] Number: {number} Title: {title}", 3, payload);
    }
}