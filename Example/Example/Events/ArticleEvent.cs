using Example.Models;
using Microsoft.Extensions.Logging;
using Zonit.Messaging.Tasks;

namespace Example.Events;

/// <summary>
/// Przykładowy handler zadania z postępem.
/// </summary>
internal class ArticleEvent(ILogger<ArticleEvent> _logger) : TaskHandler<Article>
{
    public override int WorkerCount => 5;
    public override TimeSpan Timeout => TimeSpan.FromMinutes(2);

    public override TaskProgressStep[]? ProgressSteps =>
    [
        new(TimeSpan.FromSeconds(10), "Pobieranie artykułu..."),
        new(TimeSpan.FromSeconds(15), "Przetwarzanie treści..."),
        new(TimeSpan.FromSeconds(5), "Zapisywanie...")
    ];

    protected override async Task HandleAsync(
        Article data,
        ITaskProgressContext progress,
        CancellationToken cancellationToken)
    {
        // Krok 1: Pobieranie
        await progress.NextAsync();
        await Task.Delay(10000, cancellationToken);
        _logger.LogInformation("[Article] Pobrano artykuł: {Title}", data.Title);

        // Krok 2: Przetwarzanie
        await progress.NextAsync();
        await Task.Delay(15000, cancellationToken);
        _logger.LogInformation("[Article] Przetworzono artykuł");

        // Krok 3: Zapisywanie
        await progress.NextAsync();
        await Task.Delay(5000, cancellationToken);
        _logger.LogInformation("[Article] Zapisano artykuł: {Title}", data.Title);
    }
}