using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

/// <summary>
/// Users API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _svc;

    public UsersController(IExpenseService svc) => _svc = svc;

    /// <summary>Get all users</summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var (users, error) = await _svc.GetUsersAsync();
        return Ok(new { data = users, error = error?.ToString() });
    }

    /// <summary>Get a single user by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var (user, error) = await _svc.GetUserByIdAsync(id);
        if (user == null && error == null) return NotFound();
        return Ok(new { data = user, error = error?.ToString() });
    }

    /// <summary>Create a new user</summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] UserCreateRequest request)
    {
        var (userId, error) = await _svc.CreateUserAsync(request);
        if (error != null) return StatusCode(500, new { error = error.ToString() });
        return CreatedAtAction(nameof(GetUser), new { id = userId }, new { userId });
    }
}
