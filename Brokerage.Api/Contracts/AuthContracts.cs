using FluentValidation;

namespace Brokerage.Api.Contracts;

public record LoginRequest(string Username, string Password);

public record RegisterRequest(string Username, string Password);

public record TokenResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Username).NotEmpty().MaximumLength(32);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(128);
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Username)
            .NotEmpty()
            .Length(3, 32)
            .Matches("^[A-Za-z0-9_.-]+$").WithMessage("Username can only contain letters, digits, '_', '.' and '-'.");
        RuleFor(r => r.Password)
            .NotEmpty()
            .Length(8, 128)
            .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}
