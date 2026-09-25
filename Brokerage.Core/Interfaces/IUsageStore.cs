using Brokerage.Core.Models;

namespace Brokerage.Core.Interfaces;

/// <summary>Durable per-day usage counters, so public metrics survive restarts and scale-to-zero.</summary>
public interface IUsageStore
{
    Task AddAsync(DateOnly day, UsageCounters delta, CancellationToken ct = default);
    Task<IReadOnlyList<UsageDaily>> GetRecentAsync(int days, CancellationToken ct = default);
    Task<UsageCounters> GetTotalsAsync(CancellationToken ct = default);
}
