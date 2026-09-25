using Brokerage.Core.Exceptions;
using Brokerage.Core.Models;
using Brokerage.Data;
using Brokerage.Services;

namespace Brokerage.Tests.Unit;

public class AccountServiceTests
{
    private static readonly Caller Alice = new("alice", Roles.Client);
    private static readonly Caller Bob = new("bob", Roles.Client);
    private static readonly Caller Admin = new("admin", Roles.Admin);

    private readonly InMemoryRepository<Account> _repo = new();
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        _service = new AccountService(_repo);
        _repo.AddAsync(new Account("A1", "Alice", 100M, "alice")).Wait();
        _repo.AddAsync(new Account("B1", "Bob", 100M, "bob")).Wait();
    }

    [Fact]
    public async Task Deposit_persists_the_new_balance()
    {
        await _service.Deposit("A1", 25M, Alice);
        Assert.Equal(125M, (await _repo.GetByIdAsync("A1"))!.Balance);
    }

    [Fact]
    public async Task Clients_cannot_touch_other_clients_accounts()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.Withdraw("B1", 1M, Alice));
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.GetAccount("B1", Alice));
    }

    [Fact]
    public async Task Admins_can_access_any_account()
    {
        var account = await _service.Deposit("B1", 1M, Admin);
        Assert.Equal(101M, account.Balance);
    }

    [Fact]
    public async Task GetAccounts_returns_only_the_callers_accounts_unless_admin()
    {
        Assert.Equal(["B1"], (await _service.GetAccounts(Bob)).Select(a => a.Id));
        Assert.Equal(2, (await _service.GetAccounts(Admin)).Count);
    }

    [Fact]
    public async Task Unknown_account_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.Deposit("NOPE", 1M, Alice));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CloseAccount("NOPE"));
    }

    [Fact]
    public async Task OpenAccount_assigns_an_id_and_the_caller_as_owner()
    {
        var account = await _service.OpenAccount("  Alice Two ", 10M, Alice);
        Assert.StartsWith("ACC-", account.Id);
        Assert.Equal("Alice Two", account.OwnerName);
        Assert.Equal("alice", account.OwnerUserId);
        Assert.NotNull(await _repo.GetByIdAsync(account.Id));
    }
}
