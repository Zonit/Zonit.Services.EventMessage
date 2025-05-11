namespace Zonit.Services.EventMessage;

public enum TaskEventStatus
{
    Pending = 1,        // Zadanie oczekuje na przetworzenie
    Processing,         // Zadanie jest w trakcie przetwarzania
    Completed,          // Zadanie zakończone pomyślnie
    Failed,             // Zadanie zakończone błędem
    Cancelled,          // Zadanie anulowane
    Retrying,           // Ponowna próba wykonania zadania
    Scheduled           // Zadanie zaplanowane do wykonania w przyszłości
}