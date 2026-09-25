using Azure.Monitor.OpenTelemetry.AspNetCore;
using Brokerage.Api.Infrastructure;
using Brokerage.Api.Metrics;
using Brokerage.Data;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext();
    if (builder.Environment.IsDevelopment()) logger.WriteTo.Console();
    else logger.WriteTo.Console(new RenderedCompactJsonFormatter());
}, writeToProviders: true);

// Application Insights (requests, dependencies, exceptions, logs) when a connection string is provided.
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    builder.Services.AddOpenTelemetry().UseAzureMonitor();

builder.Services.AddControllers(o => o.Filters.Add<FluentValidationFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOutputCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddBrokeragePersistence(builder.Configuration);
builder.Services.AddBrokerageServices();
builder.Services.AddBrokerageAuth(builder.Configuration);
builder.Services.AddBrokerageRateLimiting(builder.Configuration);
builder.Services.AddBrokerageSwagger();

builder.Services.AddSingleton<UsageTracker>();
builder.Services.AddHostedService<UsageFlushService>();

// Azure Container Apps terminates TLS at its ingress and forwards the original scheme and client IP.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

await InitializeDatabaseAsync(app);

app.UseForwardedHeaders();
// Outside the exception handler so it records the final status code of handled failures.
app.UseMiddleware<UsageTrackingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
    ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
    : ctx.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
    : Serilog.Events.LogEventLevel.Information);

// The public status page lives at "/" and Swagger at "/swagger" in every environment:
// this deployment is a public demo, and every money-moving endpoint requires a JWT.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(o => o.DocumentTitle = "ClearTrade Brokerage API");

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapControllers();
app.MapGet("/status", () => Results.Redirect("/")).ExcludeFromDescription();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    if (app.Configuration.HasDatabase() && app.Configuration.GetValue("Database:MigrateOnStartup", true))
        await services.GetRequiredService<BrokerageDbContext>().Database.MigrateAsync();

    var seed = app.Configuration.GetSection(SeedOptions.Section).Get<SeedOptions>() ?? new SeedOptions();
    await services.GetRequiredService<DemoDataSeeder>().SeedAsync(seed);
}

public partial class Program;
