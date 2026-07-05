using Brokerage.Core.Models;
using Brokerage.Core.Interfaces;

namespace Brokerage.Data;

public class AccountStore : IAccountRepository 
{
    private readonly List<Account> _accounts = new()
    {
        new Account("A1", "Erling Haaland", 1000.0M),
        new Account("A2", "Kylian Mbappe", 1500.0M )
    };

    public IEnumerable<Account> GetAll() => _accounts;
    public Account? GetById(string id) => _accounts.FirstOrDefault(a => a.Id == id);

    public void Add(Account account) => _accounts.Add(account);
    public void Update(Account account)
    {
        var existing =  GetById(account.Id);
        if (existing is not null) existing.Balance = account.Balance;
    }
}