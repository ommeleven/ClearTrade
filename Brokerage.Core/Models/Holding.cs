using Brokerage.Core.Interfaces;

namespace Brokerage.Core.Models;

// Not exposed through the API yet; see PRODUCTION_ROADMAP.md (Phase 1).
public class Holding : IEntity
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Ticker { get; set; } = "";
    public decimal Shares { get; set; }
    public decimal AverageCost { get; set; }
}
