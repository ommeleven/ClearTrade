using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Brokerage.Api.Infrastructure;

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}

public class RateLimitOptions
{
    public const string Section = "RateLimiting";

    public int GlobalPermitPerMinute { get; set; } = 100;
    public int AuthPermitPerMinute { get; set; } = 10;
}

public static class RateLimitingExtensions
{
    public static IServiceCollection AddBrokerageRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions();

        return services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Per-client-IP budget for everything except health probes and static files.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                ctx.Request.Path.StartsWithSegments("/api")
                    ? RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => Window(options.GlobalPermitPerMinute))
                    : RateLimitPartition.GetNoLimiter("unlimited"));

            // Tighter budget on credential endpoints to slow brute-force attempts.
            limiter.AddPolicy(RateLimitPolicies.Auth, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => Window(options.AuthPermitPerMinute)));

            limiter.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests", Detail = "Rate limit exceeded. Please retry later." },
                });
            };
        });
    }

    private static string ClientKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static FixedWindowRateLimiterOptions Window(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };
}
