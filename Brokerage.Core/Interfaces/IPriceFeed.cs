using Brokerage.Core.Models;

namespace Brokerage.Core.Interfaces;

public interface IPriceFeed
{
    decimal GetPrice(string symbol);
}