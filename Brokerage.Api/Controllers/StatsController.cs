using System.Reflection;
using Brokerage.Api.Metrics;
using Brokerage.Core.Interfaces;
using Brokerage.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Brokerage.Api.Controllers;

/// <summary>Public, anonymous usage metrics that back the /status page.</summary>
[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private const int Days = 7;

    private static readonly string Version =
        typeof(StatsController).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "dev";

    private readonly IUsageStore _store;
    private readonly UsageTracker _tracker;
    private readonly IRepository<Account> _accounts;
    private readonly IRepository<User> _users;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;

    public StatsController(IUsageStore store, UsageTracker tracker, IRepository<Account> accounts,
        IRepository<User> users, IConfiguration config, IHostEnvironment env)
    {
        _store = store;
        _tracker = tracker;
        _accounts = accounts;
        _users = users;
        _config = config;
        _env = env;
    }

    [HttpGet]
    [OutputCache(Duration = 5)]
    public async Task<StatsResponse> Get(CancellationToken ct)
    {
        var pending = _tracker.Pending;
        var totals = await _store.GetTotalsAsync(ct) + pending;
        var recent = (await _store.GetRecentAsync(Days, ct)).ToDictionary(d => d.Date, d => d.Counters);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daily = Enumerable.Range(0, Days)
            .Select(i => today.AddDays(i - (Days - 1)))
            .Select(date =>
            {
                var counters = recent.GetValueOrDefault(date) ?? new UsageCounters();
                if (date == today) counters += pending;
                return new DailyStats(date, counters.Requests, counters.Deposits + counters.Withdrawals, counters.Errors);
            })
            .ToList();

        var now = DateTimeOffset.UtcNow;
        return new StatsResponse(
            Service: "ClearTrade Brokerage API",
            Version: Version,
            Commit: _config["GIT_SHA"] is { Length: > 0 } sha ? sha[..Math.Min(7, sha.Length)] : null,
            Environment: _env.EnvironmentName,
            GeneratedAt: now,
            Instance: new InstanceStats(_tracker.StartedAt, (long)(now - _tracker.StartedAt).TotalSeconds, _tracker.GetLatency()),
            Totals: new TotalStats(
                totals.Requests, totals.Errors, totals.Logins, totals.Signups, totals.AccountsOpened,
                totals.Deposits, totals.Withdrawals, totals.DepositVolume + totals.WithdrawalVolume,
                await _accounts.CountAsync(ct), await _users.CountAsync(ct)),
            Last7Days: daily);
    }
}

public record StatsResponse(
    string Service,
    string Version,
    string? Commit,
    string Environment,
    DateTimeOffset GeneratedAt,
    InstanceStats Instance,
    TotalStats Totals,
    IReadOnlyList<DailyStats> Last7Days);

public record InstanceStats(DateTimeOffset StartedAt, long UptimeSeconds, LatencySnapshot Latency);

public record TotalStats(
    long Requests,
    long ServerErrors,
    long Logins,
    long Signups,
    long AccountsOpened,
    long Deposits,
    long Withdrawals,
    decimal VolumeMoved,
    int Accounts,
    int Users);

public record DailyStats(DateOnly Date, long Requests, long Transactions, long ServerErrors);
