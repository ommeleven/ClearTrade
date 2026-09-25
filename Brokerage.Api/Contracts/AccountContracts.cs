using Brokerage.Core.Models;
using FluentValidation;

namespace Brokerage.Api.Contracts;

public record CreateAccountRequest(string OwnerName, decimal InitialDeposit = 0M);

public record AmountRequest(decimal Amount);

public record AccountResponse(
    string Id,
    string OwnerName,
    decimal Balance,
    decimal CreditLimit,
    decimal AvailableFunds,
    bool IsOverdrawn,
    DateTimeOffset CreatedAt)
{
    public static AccountResponse From(Account a) =>
        new(a.Id, a.OwnerName, a.Balance, a.CreditLimit, a.AvailableFunds, a.IsOverdrawn, a.CreatedAt);
}

public static class MoneyRules
{
    public const decimal MaxTransaction = 1_000_000M;

    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0M).WithMessage("Amount must be greater than 0.")
            .LessThanOrEqualTo(MaxTransaction).WithMessage($"Amount cannot exceed {MaxTransaction:N0}.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Amount can have at most 2 decimal places.");
}

public class AmountRequestValidator : AbstractValidator<AmountRequest>
{
    public AmountRequestValidator()
    {
        RuleFor(r => r.Amount).ValidAmount();
    }
}

public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(r => r.OwnerName)
            .NotEmpty()
            .Must(n => n.Trim().Length >= 2).WithMessage("Owner name must be at least 2 characters.")
            .MaximumLength(Account.MaxOwnerNameLength)
            .Matches(@"^[\p{L}\p{M}' .-]+$").WithMessage("Owner name can only contain letters, spaces, apostrophes, periods and hyphens.");
        RuleFor(r => r.InitialDeposit)
            .GreaterThanOrEqualTo(0M)
            .LessThanOrEqualTo(MoneyRules.MaxTransaction)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Amount can have at most 2 decimal places.");
    }
}
