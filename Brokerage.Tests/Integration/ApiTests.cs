using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Brokerage.Api.Contracts;
using Brokerage.Api.Controllers;
using Brokerage.Api.Metrics;
using Brokerage.Core.Interfaces;
using Brokerage.Core.Models;
using Brokerage.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Brokerage.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class ApiTests : IClassFixture<ApiTests.SharedFactory>
{
    public class SharedFactory(PostgresFixture db) : IDisposable
    {
        public ApiFactory Factory { get; } = db.CreateFactory();
        public void Dispose() => Factory.Dispose();
    }

    private readonly PostgresFixture _db;
    private readonly ApiFactory _factory;

    public ApiTests(PostgresFixture db, SharedFactory shared)
    {
        _db = db;
        _factory = shared.Factory;
    }

    [Fact]
    public async Task Readiness_check_reaches_the_database()
    {
        var response = await _factory.CreateClient().GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Accounts_require_a_token()
    {
        var response = await _factory.CreateClient().GetAsync("/api/accounts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_credentials_return_401_problem()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("demo", "wrong"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Deposit_is_persisted_to_postgres()
    {
        var client = await LoginAsync("demo", "Demo@12345");
        var before = await client.GetFromJsonAsync<AccountResponse>("/api/accounts/A1");

        var response = await client.PostAsJsonAsync("/api/accounts/A1/deposit", new AmountRequest(12.34M));
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<BrokerageDbContext>().Accounts.AsNoTracking().SingleAsync(a => a.Id == "A1");
        Assert.True(stored.Balance >= before!.Balance + 12.34M);
    }

    [Fact]
    public async Task Invalid_amount_returns_400_with_field_errors()
    {
        var client = await LoginAsync("demo", "Demo@12345");
        var response = await client.PostAsJsonAsync("/api/accounts/A1/deposit", new AmountRequest(-1.001M));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Amount", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Overdraft_returns_422()
    {
        var client = await LoginAsync("demo", "Demo@12345");
        var response = await client.PostAsJsonAsync("/api/accounts/A1/withdraw", new AmountRequest(1_000_000M));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Insufficient funds", problem!.Title);
    }

    [Fact]
    public async Task Clients_get_403_on_accounts_they_do_not_own()
    {
        var client = await LoginAsync("demo", "Demo@12345");
        var response = await client.PostAsJsonAsync("/api/accounts/A2/deposit", new AmountRequest(1M));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Only_admins_can_close_accounts()
    {
        var client = await LoginAsync("demo", "Demo@12345");
        var created = await (await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Temp Owner")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/accounts/{created!.Id}")).StatusCode);

        var admin = await LoginAsync("admin", "Admin@12345");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/accounts/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/accounts/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Register_then_open_account_then_duplicate_register_conflicts()
    {
        var username = "user" + Guid.NewGuid().ToString("N")[..8];
        var anon = _factory.CreateClient();

        var register = await anon.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username, "Secret123"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var token = await register.Content.ReadFromJsonAsync<TokenResponse>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        var open = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Grace Hopper", 50M));
        Assert.Equal(HttpStatusCode.Created, open.StatusCode);
        Assert.NotNull(open.Headers.Location);

        var mine = await client.GetFromJsonAsync<List<AccountResponse>>("/api/accounts");
        Assert.Equal("Grace Hopper", Assert.Single(mine!).OwnerName);

        var duplicate = await anon.PostAsJsonAsync("/api/auth/register", new RegisterRequest(username.ToUpperInvariant(), "Secret123"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Concurrent_updates_are_detected_by_the_xmin_token()
    {
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<BrokerageDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<BrokerageDbContext>();

        var a = await db1.Accounts.SingleAsync(x => x.Id == "A2");
        var b = await db2.Accounts.SingleAsync(x => x.Id == "A2");
        a.Deposit(1M);
        b.Deposit(1M);

        await db1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Stats_count_api_traffic_and_flush_to_the_database()
    {
        var client = _factory.CreateClient();
        await client.GetAsync("/api/accounts");

        var stats = await client.GetFromJsonAsync<StatsResponse>("/api/stats", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(stats!.Totals.Requests >= 1);
        Assert.Equal(7, stats.Last7Days.Count);

        var flusher = _factory.Services.GetServices<IHostedService>().OfType<UsageFlushService>().Single();
        await flusher.FlushAsync(CancellationToken.None);
        Assert.True(_factory.Services.GetRequiredService<UsageTracker>().Pending.IsEmpty);

        using var scope = _factory.Services.CreateScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<IUsageStore>().GetTotalsAsync();
        Assert.True(persisted.Requests >= 1);
    }

    [Fact]
    public async Task Auth_endpoints_are_rate_limited()
    {
        using var limited = _db.CreateFactory(("RateLimiting:AuthPermitPerMinute", "3"));
        var client = limited.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo", "wrong"))).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.All(statuses[..3], s => Assert.Equal(HttpStatusCode.Unauthorized, s));
    }

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }
}
