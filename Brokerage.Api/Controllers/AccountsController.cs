using Brokerage.Api.Contracts;
using Brokerage.Api.Infrastructure;
using Brokerage.Api.Metrics;
using Brokerage.Core.Models;
using Brokerage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brokerage.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _service;
    private readonly UsageTracker _usage;

    public AccountsController(IAccountService service, UsageTracker usage)
    {
        _service = service;
        _usage = usage;
    }

    /// <summary>Lists the caller's accounts (all accounts for admins).</summary>
    [HttpGet]
    public async Task<IEnumerable<AccountResponse>> GetAll(CancellationToken ct)
    {
        var accounts = await _service.GetAccounts(User.ToCaller(), ct);
        return accounts.Select(AccountResponse.From);
    }

    [HttpGet("{id:maxlength(40)}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AccountResponse> GetById(string id, CancellationToken ct) =>
        AccountResponse.From(await _service.GetAccount(id, User.ToCaller(), ct));

    /// <summary>Opens a new account owned by the caller.</summary>
    [HttpPost]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountResponse>> Create(CreateAccountRequest request, CancellationToken ct)
    {
        var account = await _service.OpenAccount(request.OwnerName, request.InitialDeposit, User.ToCaller(), ct);
        _usage.Record(new UsageCounters { AccountsOpened = 1 });
        return CreatedAtAction(nameof(GetById), new { id = account.Id }, AccountResponse.From(account));
    }

    [HttpPost("{id:maxlength(40)}/deposit")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<AccountResponse> Deposit(string id, AmountRequest request, CancellationToken ct)
    {
        var account = await _service.Deposit(id, request.Amount, User.ToCaller(), ct);
        _usage.Record(new UsageCounters { Deposits = 1, DepositVolume = request.Amount });
        return AccountResponse.From(account);
    }

    [HttpPost("{id:maxlength(40)}/withdraw")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<AccountResponse> Withdraw(string id, AmountRequest request, CancellationToken ct)
    {
        var account = await _service.Withdraw(id, request.Amount, User.ToCaller(), ct);
        _usage.Record(new UsageCounters { Withdrawals = 1, WithdrawalVolume = request.Amount });
        return AccountResponse.From(account);
    }

    [HttpDelete("{id:maxlength(40)}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _service.CloseAccount(id, ct);
        return NoContent();
    }
}
