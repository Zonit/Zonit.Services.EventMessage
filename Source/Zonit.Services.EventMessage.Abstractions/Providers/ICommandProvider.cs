namespace Zonit.Services.EventMessage;

public interface ICommandProvider
{
    //Task<TResponse> Send<TResponse>(object request);
    //Task<TResponse> Send<TResponse>(string requestName, object request);
    //Task<object> Send(Type responseType, object request);
    //Task<object> Send(Type responseType, string requestName, object request);

    //// Rejestracja handlera obsługującego konkretne zapytanie
    //void Register<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handler);
    //void Register<TResponse>(string requestName, Func<object, Task<TResponse>> handler);
}
