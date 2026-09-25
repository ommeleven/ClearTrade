using Brokerage.Core.Interfaces;
using Brokerage.Core.Models;

namespace Brokerage.Api.Infrastructure;

public class SeedOptions
{
    public const string Section = "Seed";

    public string DemoUsername { get; set; } = "demo";
    public string DemoPassword { get; set; } = "Demo@12345";

    /// <summary>The admin user is only created when a password is configured (kept out of source control).</summary>
    public string? AdminPassword { get; set; }
}

/// <summary>Idempotently creates the public demo user and accounts so visitors can try the API immediately.</summary>
public class DemoDataSeeder
{
    private readonly IRepository<User> _users;
    private readonly IRepository<Account> _accounts;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(IRepository<User> users, IRepository<Account> accounts, IPasswordHasher hasher, ILogger<DemoDataSeeder> logger)
    {
        _users = users;
        _accounts = accounts;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task SeedAsync(SeedOptions options, CancellationToken ct = default)
    {
        var demo = options.DemoUsername.ToLowerInvariant();
        await EnsureUser(demo, options.DemoPassword, Roles.Client, ct);
        if (!string.IsNullOrWhiteSpace(options.AdminPassword))
            await EnsureUser("admin", options.AdminPassword, Roles.Admin, ct);

        // A1 belongs to the demo user; A2 is owned by nobody, which lets visitors see a 403 in action.
        await EnsureAccount(new Account("A1", "Erling Haaland", 1000.00M, demo) { CreditLimit = 250M }, ct);
        await EnsureAccount(new Account("A2", "Kylian Mbappe", 1500.00M), ct);
    }

    private async Task EnsureUser(string username, string password, string role, CancellationToken ct)
    {
        if (await _users.GetByIdAsync(username, ct) is not null) return;
        await _users.AddAsync(new User { Id = username, PasswordHash = _hasher.Hash(password), Role = role }, ct);
        _logger.LogInformation("Seeded {Role} user {Username}", role, username);
    }

    private async Task EnsureAccount(Account account, CancellationToken ct)
    {
        if (await _accounts.GetByIdAsync(account.Id, ct) is not null) return;
        await _accounts.AddAsync(account, ct);
    }
}
