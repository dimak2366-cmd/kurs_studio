using Microsoft.EntityFrameworkCore;

namespace Server.Data;

/// <summary>
/// Реализация обобщённого репозитория на базе Entity Framework Core.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _db;
    private readonly DbSet<T> _set;

    public Repository(AppDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _set.FindAsync(new object?[] { id }, cancellationToken).AsTask();

    public Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _set.AsNoTracking().ToListAsync(cancellationToken);

    public IQueryable<T> Query() => _set.AsQueryable();

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await _set.AddAsync(entity, cancellationToken);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
