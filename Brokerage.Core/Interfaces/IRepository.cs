using System.Security.Claims;

namespace Brokerage.Core.Interfaces;

public interface IEntity
{
    string Id { get; set; }
}

public interface IRepository<T> where T : IEntity
{
    IEnumerable<T> GetAll();
    T? GetById(string id);
    void Add(T item);
    void Update(T item);
    void Delete(string id);
}