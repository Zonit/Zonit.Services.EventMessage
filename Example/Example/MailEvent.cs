using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage;

namespace Example;

internal class MailEvent(ILogger<MailEvent> _logger) : EventBase
{
    protected override string EventName => "Article.Created";

    protected override async Task HandleAsync(object payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        _logger.LogInformation("[Mail.Send] Otrzymano: {PayloadX}", payload);
    }
}
