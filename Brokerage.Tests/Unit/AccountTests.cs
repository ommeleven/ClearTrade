using Brokerage.Core.Exceptions;
using Brokerage.Core.Models;

namespace Brokerage.Tests.Unit;

public class AccountTests
{
    [Fact]
    public void Deposit_increases_balance()
    {
        var account = new Account("A1", "Owner", 100M);
        account.Deposit(50.25M);
        Assert.Equal(150.25M, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Deposit_rejects_non_positive_amounts(decimal amount)
    {
        var account = new Account("A1", "Owner", 100M);
        Assert.Throws<DomainValidationException>(() => account.Deposit(amount));
    }

    [Fact]
    public void Deposit_rejects_amounts_that_exceed_the_balance_cap()
    {
        var account = new Account("A1", "Owner", Account.MaxBalance);
        Assert.Throws<DomainValidationException>(() => account.Deposit(1M));
    }

    [Fact]
    public void Withdraw_can_use_the_credit_limit_and_marks_account_overdrawn()
    {
        var account = new Account("A1", "Owner", 100M) { CreditLimit = 50M };
        account.Withdraw(140M);
        Assert.Equal(-40M, account.Balance);
        Assert.True(account.IsOverdrawn);
        Assert.Equal(10M, account.AvailableFunds);
    }

    [Fact]
    public void Withdraw_beyond_available_funds_throws_and_leaves_balance_unchanged()
    {
        var account = new Account("A1", "Owner", 100M) { CreditLimit = 50M };
        Assert.Throws<InsufficientFundsException>(() => account.Withdraw(150.01M));
        Assert.Equal(100M, account.Balance);
    }

    [Fact]
    public void Credit_limit_cannot_be_negative()
    {
        var account = new Account("A1", "Owner");
        Assert.Throws<DomainValidationException>(() => account.CreditLimit = -1M);
    }
}
