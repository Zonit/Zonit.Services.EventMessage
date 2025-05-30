﻿using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage;

namespace Example.Events;
internal class NotificationEvent(ILogger<NotificationEvent> _logger) : EventBase
{
    protected override string EventName => "Article.Created";
    protected override int WorkerCount => 100;

    protected override async Task HandleAsync(object payload, CancellationToken cancellationToken)
    {
        dynamic data = payload;

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