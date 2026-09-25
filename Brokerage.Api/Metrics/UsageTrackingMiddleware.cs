using System.Diagnostics;
using Brokerage.Core.Models;

namespace Brokerage.Api.Metrics;

/// <summary>Counts API traffic (excluding the stats endpoint itself, so the status page doesn't inflate its own numbers).</summary>
public class UsageTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly UsageTracker _tracker;

    public UsageTrackingMiddleware(RequestDelegate next, UsageTracker tracker)
    {
        _next = next;
        _tracker = tracker;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api") || path.StartsWithSegments("/api/stats"))
        {
            await _next(context);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        var failed = false;
        try
        {
            await _next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            _tracker.RecordLatency(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            var isError = failed || context.Response.StatusCode >= 500;
            _tracker.Record(new UsageCounters { Requests = 1, Errors = isError ? 1 : 0 });
        }
    }
}
