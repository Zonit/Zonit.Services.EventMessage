using Example.Models;
using Microsoft.Extensions.Logging;
using Zonit.Services.EventMessage;

namespace Example.Events;

internal class ArticleEvent(ILogger<MailEvent> _logger) : TaskBase<Article>
{
    protected override async Task HandleAsync(Article data, CancellationToken cancellationToken)
    {
        await Task.Delay(10000, cancellationToken);

        _logger.LogInformation("[Article] Otrzymano: {PayloadX}", data);
    }
}