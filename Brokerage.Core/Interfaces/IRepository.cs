using System.Linq.Expressions;

namespace Brokerage.Core.Interfaces;

public interface IEntity
{
    string Id { get; set; }
}

public interface IRepository<T> where T : class, IEntity
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task AddAsync(T item, CancellationToken ct = default);
    Task UpdateAsync(T item, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
