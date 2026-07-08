
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
    public async Task<IEnumerable<Account>> GetAllAccounts()
    {
        Task.FromResult(await _repo.GetAll());
    }

    public async Task<Account?> GetAccount(string id)
    {
        Task.FromResult(await _repo.GetById(id));
    }
    
    public async Task<Account> Deposit(string accountId, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Deposit amount must be positive.");

        var account = _repo.GetById(accountId) ?? 
        throw new InvalidOperationException($"Account not found for the account ID: {accountId} ");
        
        account.Balance += amount;
        await repo.Update(account);
        return Task.FromResult(account);
    }

    public async Task<Account> Withdraw(string accountId, decimal amount)
    {
         if (amount <= 0) throw new ArgumentException("Withdrawal amount must be positive.");
         
         var account = _repo.GetById(accountId) ??
         throw new InvalidOperationException($"Account not found for the account ID: {accountId} ");

        if (amount > account.Balance) throw new InvalidOperationException("Insufficient funds.");
        account.Balance -= amount;
        await _repo.Update(account);
        return Task.FromResult(account); 

       
    }
}
