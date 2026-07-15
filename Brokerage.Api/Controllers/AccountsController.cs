using Brokerage.Core.Models;
using Brokerage.Services;
using Microsoft.AspNetCore.Mvc;

namespace Brokerage.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly AccountService _service;

    public AccountsController(AccountService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IEnumerable<Account>> GetAll()
    {
      return await _service.GetAllAccounts();   
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Account>> GetById(string id)
    {
        var accounts = await _service.GetAllAccounts();
        var account = accounts.FirstOrDefault(a => a.Id == id);
        return account is null ? NotFound() : Ok(account); 
    }

    [HttpPost("{id}/deposit")]
    public async Task<ActionResult<Account>> Deposit(string id, [FromBody] decimal amount)
    {
        try {return Ok(await _service.Deposit(id, amount)); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id}/withdraw")]
    public async Task<ActionResult<Account>> Withdraw(string id, [FromBody] decimal amount)
    {
        try { return Ok(await _service.Withdraw(id, amount)); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }


}