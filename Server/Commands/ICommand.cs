namespace Server.Commands;

/// <summary>
/// Команда (паттерн «Команда»): инкапсулирует сценарий в отдельный класс,
/// который получает запрос типа TRequest и возвращает результат типа TResult.
/// </summary>
public interface ICommand<TRequest, TResult>
{
    Task<TResult> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}
