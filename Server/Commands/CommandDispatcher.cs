namespace Server.Commands;

/// <summary>
/// Диспетчер команд: находит зарегистрированную реализацию ICommand{TRequest, TResult}
/// в DI-контейнере и вызывает её.
/// </summary>
public sealed class CommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public CommandDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResult> DispatchAsync<TRequest, TResult>(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        var handler = _serviceProvider.GetService<ICommand<TRequest, TResult>>()
            ?? throw new InvalidOperationException(
                $"Не зарегистрирована команда для типа запроса {typeof(TRequest).Name} " +
                $"с результатом {typeof(TResult).Name}.");

        return handler.ExecuteAsync(request, cancellationToken);
    }
}
