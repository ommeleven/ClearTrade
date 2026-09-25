namespace Brokerage.Core.Models;

public record UsageCounters
{
    public long Requests { get; init; }
    public long Errors { get; init; }
    public long Deposits { get; init; }
    public long Withdrawals { get; init; }
    public long Logins { get; init; }
    public long Signups { get; init; }
    public long AccountsOpened { get; init; }
    public decimal DepositVolume { get; init; }
    public decimal WithdrawalVolume { get; init; }

    public bool IsEmpty => this == new UsageCounters();

    public static UsageCounters operator +(UsageCounters a, UsageCounters b) => new()
    {
        Requests = a.Requests + b.Requests,
        Errors = a.Errors + b.Errors,
        Deposits = a.Deposits + b.Deposits,
        Withdrawals = a.Withdrawals + b.Withdrawals,
        Logins = a.Logins + b.Logins,
        Signups = a.Signups + b.Signups,
        AccountsOpened = a.AccountsOpened + b.AccountsOpened,
        DepositVolume = a.DepositVolume + b.DepositVolume,
        WithdrawalVolume = a.WithdrawalVolume + b.WithdrawalVolume,
    };
}

public record UsageDaily(DateOnly Date, UsageCounters Counters);
