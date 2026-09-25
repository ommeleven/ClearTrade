using System.Collections.Concurrent;
using System.Linq.Expressions;
using Brokerage.Core.Interfaces;

namespace Brokerage.Data;

/// <summary>Process-local repository used for unit tests and for running without a database.</summary>
public class InMemoryRepository<T> : IRepository<T> where T : class, IEntity
{
    private readonly ConcurrentDictionary<string, T> _items = new();

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<T>>(_items.Values.OrderBy(i => i.Id).ToList());

    public Task<T?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<T>>(_items.Values.Where(predicate.Compile()).OrderBy(i => i.Id).ToList());

    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_items.Count);

    public Task AddAsync(T item, CancellationToken ct = default)
    {
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T item, CancellationToken ct = default)
    {
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_items.TryRemove(id, out _));
}
