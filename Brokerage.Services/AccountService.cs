
using Brokerage.Core.Models;
using Brokerage.Data;

namespace Brokerage.Services;

public class AccountService
{
    private readonly AccountStore _store;

    public AccountService(AccountStore store)
    {
        _store = store;

    }
    public IEnumerable<Account> GetAllAccounts() => _store.GetAll();

    public Account Deposit(string accountId, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Deposit amount must be positive.");

        var account = _store.GetById(accountId) ?? 
        throw new InvalidOperationException($"Account not found for the account ID: {accountId} ");
        
        account.Balance += amount;
        _store.Update(account);
        return account;
    }

    public Account Withdraw(string accountId, decimal amount)
    {
         if (amount <= 0) throw new ArgumentException("Withdrawal amount must be positive.");
         
         var account = _store.GetById(accountId) ??
         throw new InvalidOperationException($"Account not found for the account ID: {accountId} ");

        if (amount > account.Balance) throw new InvalidOperationException("Insufficient funds.");
        account.Balance -= amount;
        _store.Update(account);
        return account; 

       
    }
}
