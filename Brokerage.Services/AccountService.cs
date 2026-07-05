
using Brokerage.Core.Models;
using Brokerage.Data;

namespace Brokerage.Services;

public class AccountService
{
    private readonly AccountStore _repo;

    public AccountService(AccountStore repo)
    {
        _repo = repo;

    }
    public IEnumerable<Account> GetAllAccounts() => _repo.GetAll();

    public Account?  GetAccount(string id) => _repo.GetById(id);

    public Account Deposit(string accountId, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Deposit amount must be positive.");

        var account = _repo.GetById(accountId) ?? 
        throw new InvalidOperationException($"Account not found for the account ID: {accountId} ");
        
        account.Balance += amount;
        _repo.Update(account);
        return account;
    }

    public Account Withdraw(string accountId, decimal amount)
    {
         if (amount <= 0) throw new ArgumentException("Withdrawal amount must be positive.");
         
         var account = _repo.GetById(accountId) ??
         throw new InvalidOperationException($"Account not found for the account ID: {accountId} ");

        if (amount > account.Balance) throw new InvalidOperationException("Insufficient funds.");
        account.Balance -= amount;
        _repo.Update(account);
        return account; 

       
    }
}
