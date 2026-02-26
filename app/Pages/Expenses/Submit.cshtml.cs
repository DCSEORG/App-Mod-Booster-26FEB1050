using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages.Expenses;

public class SubmitModel : PageModel
{
    private readonly IExpenseService _svc;
    public SubmitModel(IExpenseService svc) => _svc = svc;

    public List<User> Users { get; set; } = new();
    public List<ExpenseCategory> Categories { get; set; } = new();
    public List<ExpenseStatus> Statuses { get; set; } = new();
    public ServiceError? ServiceError { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty] public ExpenseCreateRequest Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadLookupsAsync();
        Input.ExpenseDate = DateTime.UtcNow.Date;
        Input.Currency = "GBP";
        Input.StatusId = 2; // default: Submitted
    }

    public async Task<IActionResult> OnPostAsync(decimal AmountGBP)
    {
        await LoadLookupsAsync();

        if (!ModelState.IsValid)
            return Page();

        // Convert £ to pence
        Input.AmountMinor = (int)Math.Round(AmountGBP * 100);
        Input.Currency = "GBP";

        // Set SubmittedAt if status is Submitted
        if (Input.StatusId == 2)
            Input.SubmittedAt = DateTime.UtcNow;

        var (expenseId, error) = await _svc.CreateExpenseAsync(Input);
        if (error != null)
        {
            ServiceError = error;
            return Page();
        }

        SuccessMessage = $"Expense #{expenseId} submitted successfully.";
        return Page();
    }

    private async Task LoadLookupsAsync()
    {
        var (users, uErr) = await _svc.GetUsersAsync();
        var (cats, cErr) = await _svc.GetCategoriesAsync();
        var (statuses, sErr) = await _svc.GetStatusesAsync();
        Users = users;
        Categories = cats;
        Statuses = statuses;
        ServiceError = uErr ?? cErr ?? sErr;
    }
}
