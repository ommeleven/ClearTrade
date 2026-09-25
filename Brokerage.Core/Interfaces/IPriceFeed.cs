namespace Brokerage.Core.Interfaces;

public interface IPriceFeed
{
    decimal? GetPrice(string symbol);
}
