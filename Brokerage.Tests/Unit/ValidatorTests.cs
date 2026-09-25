using Brokerage.Api.Contracts;

namespace Brokerage.Tests.Unit;

public class ValidatorTests
{
    [Theory]
    [InlineData(0.01, true)]
    [InlineData(1_000_000, true)]
    [InlineData(100.10, true)]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1_000_000.01, false)]
    [InlineData(1.005, false)]
    public void Amount_rules(decimal amount, bool valid)
    {
        var result = new AmountRequestValidator().Validate(new AmountRequest(amount));
        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("Ada Lovelace", true)]
    [InlineData("Zoë O'Brien-Smith", true)]
    [InlineData("A", false)]
    [InlineData("   ", false)]
    [InlineData("<script>", false)]
    public void Owner_name_rules(string name, bool valid)
    {
        var result = new CreateAccountRequestValidator().Validate(new CreateAccountRequest(name));
        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("founder", "Secret123", true)]
    [InlineData("ab", "Secret123", false)]
    [InlineData("bad name", "Secret123", false)]
    [InlineData("founder", "short1", false)]
    [InlineData("founder", "nodigitshere", false)]
    public void Registration_rules(string username, string password, bool valid)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest(username, password));
        Assert.Equal(valid, result.IsValid);
    }
}
