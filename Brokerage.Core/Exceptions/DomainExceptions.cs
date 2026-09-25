namespace Brokerage.Core.Exceptions;

/// <summary>Base type for expected business-rule failures; mapped to ProblemDetails by the API.</summary>
public abstract class DomainException(string message) : Exception(message);

public class DomainValidationException(string message) : DomainException(message);

public class NotFoundException(string resource, string id)
    : DomainException($"{resource} '{id}' was not found.");

public class ConflictException(string message) : DomainException(message);

public class ForbiddenException(string message) : DomainException(message);

public class InsufficientFundsException(string accountId, decimal requested, decimal available)
    : DomainException($"Insufficient funds in account '{accountId}': requested {requested:0.00}, available {available:0.00}.");
