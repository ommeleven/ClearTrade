using Brokerage.Core.Models;

namespace Brokerage.Core.Interfaces;

public interface IAccountRepository
{
    IEnumerable<Account> GetAll();
    Account? GetById(string id);
    void Add(Account account);
    void Update(Account account);
}