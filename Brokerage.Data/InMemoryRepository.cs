using Brokerage.Core.Interfaces;
using Brokerage.Data;


namespace Brokerage.Data;

public class InMemoryRepository<T> : InRepository<T> where T : IEntity
{
    private readonly Dictionary<string, T> _items = new();

    public Task<IEnumerable<T>> GetAllAsync() 
    {
        var allItems = _items.Values;
        return Task.FromResult(allItems);
    }

    public Task<T?> GetByIdAsync(string id)
    {
        var item = _items.GetValueOrDefault(id);
        return Task.FromResult(item);
    }

    public Task AddAsync(T item)
    {
        _items[item.id] = item;  
        Task.CompletedTask;
    } 

    public Task UpdateAsync(T item)
    {
        _items[item.id] = item;
        Task.CompletedTask;
    }

    public async Task DeleteAsync(string id)
    {
        _items.Remove(id);
        return Task.CompletedTask;
    }
}