using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages.Expenses;

public class ReviewModel : PageModel
{
    private readonly IExpenseService _svc;
    public ReviewModel(IExpenseService svc) => _svc = svc;

    public Expense? Expense { get; set; }
    public List<User> Managers { get; set; } = new();
    public ServiceError? ServiceError { get; set; }

    public async Task OnGetAsync([FromQuery] int id)
    {
        await LoadAsync(id);
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, int reviewerId)
    {
        var (success, error) = await _svc.UpdateExpenseStatusAsync(id, new ExpenseStatusUpdateRequest
        {
            StatusId = 3, // Approved
            ReviewedBy = reviewerId
        });
        if (error != null)
        {
            ServiceError = error;
            await LoadAsync(id);
            return Page();
        }
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, int reviewerId)
    {
        var (success, error) = await _svc.UpdateExpenseStatusAsync(id, new ExpenseStatusUpdateRequest
        {
            StatusId = 4, // Rejected
            ReviewedBy = reviewerId
        });
        if (error != null)
        {
            ServiceError = error;
            await LoadAsync(id);
            return Page();
        }
        return RedirectToPage("/Index");
    }

    private async Task LoadAsync(int id)
    {
        var (expense, expError) = await _svc.GetExpenseByIdAsync(id);
        var (users, usersError) = await _svc.GetUsersAsync();

        Expense = expense;
        Managers = users.Where(u => u.RoleName == "Manager").ToList();
        ServiceError = expError ?? usersError;
    }
}
