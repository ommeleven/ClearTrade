namespace Brokerage.Core.Models;

public class Account
{
    public string Id { get; set; }
    public string OwnerName { get;  set; }
    public decimal Balance { get;  set; }
    public bool isOverDrawn => Balance < 0;
    
    public decimal _credtLimit;
    public decimal CredtLimit
    {
        get => _credtLimit;
        set
        {
            if (value < 0) throw new ArgumentException("Credit Limit cannot be negative.");
            _credtLimit = value;
        }
    }

    public Account(string id, string ownerName, decimal? balance)
    {
        Id = id;
        OwnerName = ownerName;
        Balance = balance ?? 0M;
    }

}
