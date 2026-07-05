using Brokerage.Core.Interfaces;

namespace Brokerage.Data; 

public class StubPriceFeed : IPriceFeed
{
    private readonly Dictionary<string, decimal> _prices = new()
    {
      ["AAPL"] = "192.50M",
      ["MSFT"] = "402.00M",
      ["TSLA"] = "282.50M",
        
    };

    public decimal GetPrice(string symbol) => _prices.GetValueOrDefault(symbol, 0M);
    
}