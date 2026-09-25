using Brokerage.Core.Models;

namespace Brokerage.Core.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenIssuer
{
    AuthToken Issue(User user);
}
