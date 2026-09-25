using Brokerage.Core.Interfaces;

namespace Brokerage.Api.Metrics;

/// <summary>Periodically persists in-memory usage counters so public metrics survive restarts and scale-to-zero.</summary>
public class UsageFlushService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly UsageTracker _tracker;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<UsageFlushService> _logger;

    public UsageFlushService(UsageTracker tracker, IServiceScopeFactory scopes, ILogger<UsageFlushService> logger)
    {
        _tracker = tracker;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await FlushAsync(cancellationToken);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        var pending = _tracker.TakePending();
        if (pending.IsEmpty) return;

        try
        {
            using var scope = _scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IUsageStore>();
            await store.AddAsync(DateOnly.FromDateTime(DateTime.UtcNow), pending, ct);
        }
        catch (Exception ex)
        {
            // Put the counts back so they are retried on the next tick rather than lost.
            _tracker.Record(pending);
            _logger.LogWarning(ex, "Failed to flush usage counters; will retry.");
        }
    }
}
