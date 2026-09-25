using System.Linq.Expressions;
using Brokerage.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Data;

public class EfRepository<T> : IRepository<T> where T : class, IEntity
{
    private readonly BrokerageDbContext _db;
    private readonly DbSet<T> _set;

    public EfRepository(BrokerageDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
        await _set.AsNoTracking().OrderBy(i => i.Id).ToListAsync(ct);

    public Task<T?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _set.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        await _set.AsNoTracking().Where(predicate).OrderBy(i => i.Id).ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => _set.CountAsync(ct);

    public async Task AddAsync(T item, CancellationToken ct = default)
    {
        _set.Add(item);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(T item, CancellationToken ct = default)
    {
        if (_db.Entry(item).State == EntityState.Detached) _set.Update(item);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default) =>
        await _set.Where(i => i.Id == id).ExecuteDeleteAsync(ct) > 0;
}
