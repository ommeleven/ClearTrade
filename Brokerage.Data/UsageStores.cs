using System.Collections.Concurrent;
using Brokerage.Core.Interfaces;
using Brokerage.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Data;

public class EfUsageStore : IUsageStore
{
    private readonly BrokerageDbContext _db;

    public EfUsageStore(BrokerageDbContext db)
    {
        _db = db;
    }

    // Atomic upsert so concurrent replicas can flush into the same day's row.
    public Task AddAsync(DateOnly day, UsageCounters d, CancellationToken ct = default) =>
        _db.Database.ExecuteSqlAsync($"""
            INSERT INTO usage_daily (date, requests, errors, deposits, withdrawals, logins, signups, accounts_opened, deposit_volume, withdrawal_volume)
            VALUES ({day}, {d.Requests}, {d.Errors}, {d.Deposits}, {d.Withdrawals}, {d.Logins}, {d.Signups}, {d.AccountsOpened}, {d.DepositVolume}, {d.WithdrawalVolume})
            ON CONFLICT (date) DO UPDATE SET
                requests = usage_daily.requests + EXCLUDED.requests,
                errors = usage_daily.errors + EXCLUDED.errors,
                deposits = usage_daily.deposits + EXCLUDED.deposits,
                withdrawals = usage_daily.withdrawals + EXCLUDED.withdrawals,
                logins = usage_daily.logins + EXCLUDED.logins,
                signups = usage_daily.signups + EXCLUDED.signups,
                accounts_opened = usage_daily.accounts_opened + EXCLUDED.accounts_opened,
                deposit_volume = usage_daily.deposit_volume + EXCLUDED.deposit_volume,
                withdrawal_volume = usage_daily.withdrawal_volume + EXCLUDED.withdrawal_volume
            """, ct);

    public async Task<IReadOnlyList<UsageDaily>> GetRecentAsync(int days, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(days - 1));
        var rows = await _db.UsageDaily.AsNoTracking().Where(r => r.Date >= from).OrderBy(r => r.Date).ToListAsync(ct);
        return rows.Select(r => new UsageDaily(r.Date, r.ToCounters())).ToList();
    }

    public async Task<UsageCounters> GetTotalsAsync(CancellationToken ct = default)
    {
        var rows = await _db.UsageDaily.AsNoTracking().ToListAsync(ct);
        return rows.Aggregate(new UsageCounters(), (sum, r) => sum + r.ToCounters());
    }
}

public class InMemoryUsageStore : IUsageStore
{
    private readonly ConcurrentDictionary<DateOnly, UsageCounters> _days = new();

    public Task AddAsync(DateOnly day, UsageCounters delta, CancellationToken ct = default)
    {
        _days.AddOrUpdate(day, delta, (_, existing) => existing + delta);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UsageDaily>> GetRecentAsync(int days, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(days - 1));
        IReadOnlyList<UsageDaily> result = _days.Where(kv => kv.Key >= from).OrderBy(kv => kv.Key)
            .Select(kv => new UsageDaily(kv.Key, kv.Value)).ToList();
        return Task.FromResult(result);
    }

    public Task<UsageCounters> GetTotalsAsync(CancellationToken ct = default) =>
        Task.FromResult(_days.Values.Aggregate(new UsageCounters(), (sum, c) => sum + c));
}
