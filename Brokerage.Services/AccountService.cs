using Brokerage.Core.Exceptions;
using Brokerage.Core.Interfaces;
using Brokerage.Core.Models;

namespace Brokerage.Services;

public interface IAccountService
{
    Task<IReadOnlyList<Account>> GetAccounts(Caller caller, CancellationToken ct = default);
    Task<Account> GetAccount(string id, Caller caller, CancellationToken ct = default);
    Task<Account> OpenAccount(string ownerName, decimal initialDeposit, Caller caller, CancellationToken ct = default);
    Task<Account> Deposit(string accountId, decimal amount, Caller caller, CancellationToken ct = default);
    Task<Account> Withdraw(string accountId, decimal amount, Caller caller, CancellationToken ct = default);
    Task CloseAccount(string accountId, CancellationToken ct = default);
}

public class AccountService : IAccountService
{
    private readonly IRepository<Account> _repo;

    public AccountService(IRepository<Account> repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<Account>> GetAccounts(Caller caller, CancellationToken ct = default) =>
        caller.IsAdmin ? _repo.GetAllAsync(ct) : _repo.FindAsync(a => a.OwnerUserId == caller.UserId, ct);

    public Task<Account> GetAccount(string id, Caller caller, CancellationToken ct = default) =>
        GetOwnedAccount(id, caller, ct);

    public async Task<Account> OpenAccount(string ownerName, decimal initialDeposit, Caller caller, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerName)) throw new DomainValidationException("Owner name is required.");
        if (initialDeposit < 0) throw new DomainValidationException("Initial deposit cannot be negative.");

        var account = new Account(NewAccountId(), ownerName.Trim(), initialDeposit, caller.UserId);
        await _repo.AddAsync(account, ct);
        return account;
    }

    public async Task<Account> Deposit(string accountId, decimal amount, Caller caller, CancellationToken ct = default)
    {
        var account = await GetOwnedAccount(accountId, caller, ct);
        account.Deposit(amount);
        await _repo.UpdateAsync(account, ct);
        return account;
    }

    public async Task<Account> Withdraw(string accountId, decimal amount, Caller caller, CancellationToken ct = default)
    {
        var account = await GetOwnedAccount(accountId, caller, ct);
        account.Withdraw(amount);
        await _repo.UpdateAsync(account, ct);
        return account;
    }

    public async Task CloseAccount(string accountId, CancellationToken ct = default)
    {
        if (!await _repo.DeleteAsync(accountId, ct)) throw new NotFoundException("Account", accountId);
    }

    private async Task<Account> GetOwnedAccount(string accountId, Caller caller, CancellationToken ct)
    {
        var account = await _repo.GetByIdAsync(accountId, ct) ?? throw new NotFoundException("Account", accountId);
        if (!caller.IsAdmin && account.OwnerUserId != caller.UserId)
            throw new ForbiddenException($"Account '{accountId}' does not belong to the current user.");
        return account;
    }

    private static string NewAccountId() => "ACC-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
}
