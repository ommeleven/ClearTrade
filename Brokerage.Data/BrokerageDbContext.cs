using Brokerage.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Data;

public class BrokerageDbContext : DbContext
{
    public BrokerageDbContext(DbContextOptions<BrokerageDbContext> options) : base(options) {}

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UsageDailyRow> UsageDaily => Set<UsageDailyRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasMaxLength(40);
            e.Property(a => a.OwnerName).HasMaxLength(Account.MaxOwnerNameLength).IsRequired();
            e.Property(a => a.OwnerUserId).HasMaxLength(64);
            e.Property(a => a.Balance).HasPrecision(18, 2);
            e.Property(a => a.CreditLimit).HasPrecision(18, 2);
            e.Property(a => a.Version).IsRowVersion();
            e.HasIndex(a => a.OwnerUserId);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasMaxLength(64);
            e.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<UsageDailyRow>(e =>
        {
            e.ToTable("usage_daily");
            e.HasKey(u => u.Date);
            e.Property(u => u.Date).HasColumnName("date");
            e.Property(u => u.Requests).HasColumnName("requests");
            e.Property(u => u.Errors).HasColumnName("errors");
            e.Property(u => u.Deposits).HasColumnName("deposits");
            e.Property(u => u.Withdrawals).HasColumnName("withdrawals");
            e.Property(u => u.Logins).HasColumnName("logins");
            e.Property(u => u.Signups).HasColumnName("signups");
            e.Property(u => u.AccountsOpened).HasColumnName("accounts_opened");
            e.Property(u => u.DepositVolume).HasColumnName("deposit_volume").HasPrecision(18, 2);
            e.Property(u => u.WithdrawalVolume).HasColumnName("withdrawal_volume").HasPrecision(18, 2);
        });
    }
}

public class UsageDailyRow
{
    public DateOnly Date { get; set; }
    public long Requests { get; set; }
    public long Errors { get; set; }
    public long Deposits { get; set; }
    public long Withdrawals { get; set; }
    public long Logins { get; set; }
    public long Signups { get; set; }
    public long AccountsOpened { get; set; }
    public decimal DepositVolume { get; set; }
    public decimal WithdrawalVolume { get; set; }

    public UsageCounters ToCounters() => new()
    {
        Requests = Requests, Errors = Errors, Deposits = Deposits, Withdrawals = Withdrawals,
        Logins = Logins, Signups = Signups, AccountsOpened = AccountsOpened,
        DepositVolume = DepositVolume, WithdrawalVolume = WithdrawalVolume,
    };
}
