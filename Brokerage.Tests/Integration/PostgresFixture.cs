using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Brokerage.Tests.Integration;

/// <summary>One real PostgreSQL container shared by all integration tests.</summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public ApiFactory CreateFactory(params (string Key, string Value)[] overrides) => new(ConnectionString, overrides);
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

public class ApiFactory(string connectionString, (string Key, string Value)[] overrides) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:BrokerageDb", connectionString);
        builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Seed:AdminPassword", "Admin@12345");
        builder.UseSetting("RateLimiting:GlobalPermitPerMinute", "10000");
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "10000");
        foreach (var (key, value) in overrides) builder.UseSetting(key, value);
    }
}
