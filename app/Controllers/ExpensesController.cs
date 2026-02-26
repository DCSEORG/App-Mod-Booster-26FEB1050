using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

/// <summary>
/// Expenses API – CRUD operations for expense records
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _svc;

    public ExpensesController(IExpenseService svc) => _svc = svc;

    /// <summary>Get all expenses with optional filters</summary>
    [HttpGet]
    public async Task<IActionResult> GetExpenses([FromQuery] int? userId, [FromQuery] int? statusId, [FromQuery] int? categoryId)
    {
        var (expenses, error) = await _svc.GetExpensesAsync(userId, statusId, categoryId);
        return Ok(new { data = expenses, error = error?.ToString() });
    }

    /// <summary>Get a single expense by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetExpense(int id)
    {
        var (expense, error) = await _svc.GetExpenseByIdAsync(id);
        if (expense == null && error == null) return NotFound();
        return Ok(new { data = expense, error = error?.ToString() });
    }

    /// <summary>Create a new expense</summary>
    [HttpPost]
    public async Task<IActionResult> CreateExpense([FromBody] ExpenseCreateRequest request)
    {
        var (expenseId, error) = await _svc.CreateExpenseAsync(request);
        if (error != null) return StatusCode(500, new { error = error.ToString() });
        return CreatedAtAction(nameof(GetExpense), new { id = expenseId }, new { expenseId });
    }

    /// <summary>Update the status of an expense (approve / reject)</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] ExpenseStatusUpdateRequest request)
    {
        var (success, error) = await _svc.UpdateExpenseStatusAsync(id, request);
        if (error != null) return StatusCode(500, new { error = error.ToString() });
        if (!success) return NotFound();
        return NoContent();
    }
}
