using Brokerage.Core.Exceptions;
using Brokerage.Core.Interfaces;
using Brokerage.Core.Models;

namespace Brokerage.Services;

public interface IAuthService
{
    Task<AuthToken> Register(string username, string password, CancellationToken ct = default);
    Task<AuthToken?> Login(string username, string password, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IRepository<User> _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenIssuer _tokens;

    public AuthService(IRepository<User> users, IPasswordHasher hasher, ITokenIssuer tokens)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<AuthToken> Register(string username, string password, CancellationToken ct = default)
    {
        var id = Normalize(username);
        if (await _users.GetByIdAsync(id, ct) is not null)
            throw new ConflictException($"Username '{id}' is already taken.");

        var user = new User { Id = id, PasswordHash = _hasher.Hash(password), Role = Roles.Client };
        await _users.AddAsync(user, ct);
        return _tokens.Issue(user);
    }

    public async Task<AuthToken?> Login(string username, string password, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(Normalize(username), ct);
        if (user is null || !_hasher.Verify(password, user.PasswordHash)) return null;
        return _tokens.Issue(user);
    }

    public static string Normalize(string username) => username.Trim().ToLowerInvariant();
}
