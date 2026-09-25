using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brokerage.Data;

// Lets `dotnet ef migrations add` run against this project without booting the API.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BrokerageDbContext>
{
    public BrokerageDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__BrokerageDb")
            ?? "Host=localhost;Database=brokerage;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<BrokerageDbContext>().UseNpgsql(connectionString).Options;
        return new BrokerageDbContext(options);
    }
}
