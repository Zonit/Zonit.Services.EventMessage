namespace Zonit.Messaging.Tasks;

/// <summary>
/// Metody rozszerzaj¹ce dla ITaskProvider.
/// </summary>
public static class TaskProviderExtensions
{
    /// <summary>
    /// Publikuje zadanie z opcjonalnym identyfikatorem rozszerzenia.
    /// Jeœli extensionId jest null, publikuje zadanie bez identyfikatora rozszerzenia.
    /// </summary>
    /// <typeparam name="TTask">Typ zadania</typeparam>
    /// <param name="provider">Provider zadañ</param>
    /// <param name="payload">Dane zadania</param>
    /// <param name="extensionId">Opcjonalny identyfikator rozszerzenia</param>
    public static void Publish<TTask>(this ITaskProvider provider, TTask payload, Guid? extensionId) 
        where TTask : notnull
    {
        if (extensionId.HasValue)
        {
            provider.Publish(payload, extensionId.Value);
        }
        else
        {
            provider.Publish(payload);
        }
    }
}
