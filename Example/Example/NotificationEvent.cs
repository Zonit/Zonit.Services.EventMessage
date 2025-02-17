using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage;

namespace Example;
internal class NotificationEvent(ILogger<NotificationEvent> _logger) : EventBase
{
    protected override string EventName => "Article.Created";
    protected override int EventWorker => 100;

    protected override async Task HandleAsync(PayloadModel payload, CancellationToken cancellationToken)
    {
        dynamic data = payload.Data;

        _logger.LogInformation($"[NotificationEvent] {data.Name} {data.Context}");
        await Task.Delay(1000, cancellationToken);

        //if (payload.Data is Test1 message)
        //{
        //    _logger.LogInformation("[NotificationEvent] Wysłano powiadomienie: {Name} {Context}", message.Name, message.Context);
        //    await Task.Delay(1000, cancellationToken);
        //}
        //else
        //{
        //    _logger.LogWarning("[NotificationEvent] Otrzymano niepoprawny ładunek: {Payload}", payload);
        //}
    }
}