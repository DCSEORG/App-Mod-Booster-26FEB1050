using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

/// <summary>
/// Lookup data – categories and statuses
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _svc;
    public CategoriesController(IExpenseService svc) => _svc = svc;

    /// <summary>Get all expense categories</summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var (cats, error) = await _svc.GetCategoriesAsync();
        return Ok(new { data = cats, error = error?.ToString() });
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatusesController : ControllerBase
{
    private readonly IExpenseService _svc;
    public StatusesController(IExpenseService svc) => _svc = svc;

    /// <summary>Get all expense statuses</summary>
    [HttpGet]
    public async Task<IActionResult> GetStatuses()
    {
        var (statuses, error) = await _svc.GetStatusesAsync();
        return Ok(new { data = statuses, error = error?.ToString() });
    }
}
