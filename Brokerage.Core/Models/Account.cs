using Brokerage.Core.Exceptions;
using Brokerage.Core.Interfaces;

namespace Brokerage.Core.Models;

public class Account : IEntity
{
    public const int MaxOwnerNameLength = 100;
    public const decimal MaxBalance = 1_000_000_000M;

    private decimal _creditLimit;

    public string Id { get; set; } = "";
    public string OwnerName { get; set; } = "";

    /// <summary>User that owns this account; null for system-owned accounts.</summary>
    public string? OwnerUserId { get; set; }

    public decimal Balance { get; private set; }

    public decimal CreditLimit
    {
        get => _creditLimit;
        set
        {
            if (value < 0) throw new DomainValidationException("Credit limit cannot be negative.");
            _creditLimit = value;
        }
    }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optimistic concurrency token (mapped to Postgres xmin).</summary>
    public uint Version { get; set; }

    public bool IsOverdrawn => Balance < 0;
    public decimal AvailableFunds => Balance + CreditLimit;

    // Required by EF Core.
    private Account() { }

    public Account(string id, string ownerName, decimal? balance = null, string? ownerUserId = null)
    {
        Id = id;
        OwnerName = ownerName;
        Balance = balance ?? 0M;
        OwnerUserId = ownerUserId;
    }

    public void Deposit(decimal amount)
    {
        if (amount <= 0) throw new DomainValidationException("Deposit amount must be positive.");
        if (Balance + amount > MaxBalance) throw new DomainValidationException($"Balance cannot exceed {MaxBalance:N0}.");
        Balance += amount;
    }

    public void Withdraw(decimal amount)
    {
        if (amount <= 0) throw new DomainValidationException("Withdrawal amount must be positive.");
        if (amount > AvailableFunds) throw new InsufficientFundsException(Id, amount, AvailableFunds);
        Balance -= amount;
    }
}
