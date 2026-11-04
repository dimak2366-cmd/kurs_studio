namespace Server.Data;

/// <summary>
/// Обобщённый репозиторий (паттерн «Репозиторий»).
/// Инкапсулирует доступ к набору сущностей T и не раскрывает подробности EF Core наружу.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    IQueryable<T> Query();
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
