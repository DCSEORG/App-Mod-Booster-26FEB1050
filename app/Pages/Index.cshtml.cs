using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _svc;

    public IndexModel(IExpenseService svc) => _svc = svc;

    public List<Expense> Expenses { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();
    public ServiceError? ServiceError { get; set; }

    [BindProperty(SupportsGet = true)] public int? StatusId { get; set; }
    [BindProperty(SupportsGet = true)] public int? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }

    public int? SelectedStatusId => StatusId;
    public int? SelectedUserId => UserId;
    public int? SelectedCategoryId => CategoryId;

    public async Task OnGetAsync()
    {
        var (expenses, expError) = await _svc.GetExpensesAsync(UserId, StatusId, CategoryId);
        var (users, usersError) = await _svc.GetUsersAsync();
        var (categories, catError) = await _svc.GetCategoriesAsync();
        var (statuses, statError) = await _svc.GetStatusesAsync();

        Expenses = expenses;
        Users = users;
        Categories = categories;
        Statuses = statuses;
        ServiceError = expError ?? usersError ?? catError ?? statError;
    }
}
