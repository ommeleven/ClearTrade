using Brokerage.Core.Models;
using Brokerage.Data;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Data;

public class BrokerageDbContext : DbContext
{
    public BrokerageDbContext(DbContextOptions<BrokerageDbContext> options) : base(options) {}

    // public DbSet<>
}