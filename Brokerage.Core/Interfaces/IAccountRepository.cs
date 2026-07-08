using Brokerage.Core.Models;

namespace Brokerage.Core.Interfaces;

public interface IAccountRepository
{
    Task<T> GetAll();
    Task GetById(string id);
    Task Add(Account account);
    Task Update(Account account);
}