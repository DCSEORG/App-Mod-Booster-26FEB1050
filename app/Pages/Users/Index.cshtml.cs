using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages.Users;

public class IndexModel : PageModel
{
    private readonly IExpenseService _svc;
    public IndexModel(IExpenseService svc) => _svc = svc;

    public List<User> Users { get; set; } = new();
    public ServiceError? ServiceError { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty] public UserCreateRequest Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        var (users, error) = await _svc.GetUsersAsync();
        Users = users;
        ServiceError = error;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            var (users, _) = await _svc.GetUsersAsync();
            Users = users;
            return Page();
        }

        var (userId, error) = await _svc.CreateUserAsync(Input);
        if (error != null)
        {
            ServiceError = error;
            var (users, _) = await _svc.GetUsersAsync();
            Users = users;
            return Page();
        }

        SuccessMessage = $"User created successfully (ID #{userId}).";
        var (refreshed, _) = await _svc.GetUsersAsync();
        Users = refreshed;
        return Page();
    }
}
