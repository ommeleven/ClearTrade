using Brokerage.Core.Models;

namespace Brokerage.Api.Metrics;

/// <summary>In-process usage counters and a rolling latency window. Counters are periodically flushed to <see cref="Brokerage.Core.Interfaces.IUsageStore"/>.</summary>
public class UsageTracker
{
    private const int LatencyWindow = 2048;

    private readonly object _lock = new();
    private readonly double[] _latencies = new double[LatencyWindow];
    private int _latencyCount;
    private int _latencyNext;
    private UsageCounters _pending = new();

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public void Record(UsageCounters delta)
    {
        lock (_lock) _pending += delta;
    }

    public void RecordLatency(double milliseconds)
    {
        lock (_lock)
        {
            _latencies[_latencyNext] = milliseconds;
            _latencyNext = (_latencyNext + 1) % LatencyWindow;
            _latencyCount = Math.Min(_latencyCount + 1, LatencyWindow);
        }
    }

    public UsageCounters Pending
    {
        get { lock (_lock) return _pending; }
    }

    public UsageCounters TakePending()
    {
        lock (_lock)
        {
            var pending = _pending;
            _pending = new UsageCounters();
            return pending;
        }
    }

    public LatencySnapshot GetLatency()
    {
        double[] samples;
        lock (_lock) samples = _latencies[.._latencyCount];
        if (samples.Length == 0) return new LatencySnapshot(0, null, null);

        Array.Sort(samples);
        return new LatencySnapshot(samples.Length, Percentile(samples, 0.50), Percentile(samples, 0.95));
    }

    private static double Percentile(double[] sorted, double p) =>
        Math.Round(sorted[(int)Math.Ceiling(p * sorted.Length) - 1], 1);
}

public record LatencySnapshot(int SampleSize, double? P50Ms, double? P95Ms);
