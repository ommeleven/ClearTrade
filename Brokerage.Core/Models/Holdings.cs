namespace Brokerage.Core.Models;

public class Holdng : IEntity
{
    public string Id { get; set; }
    public string AccountId { get; set; }
    public string Ticker { get; set; }
    public string Shares { get; set; }
    public decimal AverageCost { get; set; }
    

}