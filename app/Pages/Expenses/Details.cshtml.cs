using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages.Expenses;

public class DetailsModel : PageModel
{
    private readonly IExpenseService _svc;
    public DetailsModel(IExpenseService svc) => _svc = svc;

    public Expense? Expense { get; set; }
    public ServiceError? ServiceError { get; set; }

    public async Task OnGetAsync([FromQuery] int id)
    {
        var (expense, error) = await _svc.GetExpenseByIdAsync(id);
        Expense = expense;
        ServiceError = error;
    }
}
