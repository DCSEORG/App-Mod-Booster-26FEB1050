using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

/// <summary>
/// Captures details of a database/service error so the UI can show a helpful error bar.
/// </summary>
public class ServiceError
{
    public string Message { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string? ManagedIdentityAdvice { get; set; }

    public override string ToString() =>
        $"Error in {FileName} (line {LineNumber}, {MemberName}): {Message}" +
        (ManagedIdentityAdvice is not null ? $" | {ManagedIdentityAdvice}" : "");
}

public interface IExpenseService
{
    Task<(List<Expense> Expenses, ServiceError? Error)> GetExpensesAsync(int? userId = null, int? statusId = null, int? categoryId = null);
    Task<(Expense? Expense, ServiceError? Error)> GetExpenseByIdAsync(int expenseId);
    Task<(int? ExpenseId, ServiceError? Error)> CreateExpenseAsync(ExpenseCreateRequest request);
    Task<(bool Success, ServiceError? Error)> UpdateExpenseStatusAsync(int expenseId, ExpenseStatusUpdateRequest request);
    Task<(List<User> Users, ServiceError? Error)> GetUsersAsync();
    Task<(User? User, ServiceError? Error)> GetUserByIdAsync(int userId);
    Task<(int? UserId, ServiceError? Error)> CreateUserAsync(UserCreateRequest request);
    Task<(List<ExpenseCategory> Categories, ServiceError? Error)> GetCategoriesAsync();
    Task<(List<ExpenseStatus> Statuses, ServiceError? Error)> GetStatusesAsync();
}

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _config;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration config, ILogger<ExpenseService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private SqlConnection CreateConnection()
    {
        var connectionString = _config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");
        return new SqlConnection(connectionString);
    }

    private ServiceError BuildError(Exception ex,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "")
    {
        _logger.LogError(ex, "Database error at {File}:{Line} in {Member}", file, line, member);
        string? advice = null;
        if (ex.Message.Contains("Cannot open server") || ex.Message.Contains("Login failed") ||
            ex.Message.Contains("token") || ex.Message.Contains("identity"))
        {
            advice = "Managed Identity issue detected. " +
                     "Ensure the App Service has a user-assigned managed identity and AZURE_CLIENT_ID is set to its client ID. " +
                     "The connection string must use 'Authentication=Active Directory Managed Identity;User Id=<client-id>;'. " +
                     "For local development use 'Authentication=Active Directory Default' in appsettings.Development.json and run 'az login'.";
        }
        return new ServiceError
        {
            Message = ex.Message,
            FileName = System.IO.Path.GetFileName(file),
            LineNumber = line,
            MemberName = member,
            ManagedIdentityAdvice = advice
        };
    }

    // -------------------------------------------------
    // EXPENSES
    // -------------------------------------------------

    public async Task<(List<Expense> Expenses, ServiceError? Error)> GetExpensesAsync(
        int? userId = null, int? statusId = null, int? categoryId = null)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenses", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);

            var expenses = new List<Expense>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                expenses.Add(MapExpense(reader));
            return (expenses, null);
        }
        catch (Exception ex)
        {
            return (GetDummyExpenses(), BuildError(ex));
        }
    }

    public async Task<(Expense? Expense, ServiceError? Error)> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseById", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (MapExpense(reader), null);
            return (null, null);
        }
        catch (Exception ex)
        {
            return (GetDummyExpenses().FirstOrDefault(), BuildError(ex));
        }
    }

    public async Task<(int? ExpenseId, ServiceError? Error)> CreateExpenseAsync(ExpenseCreateRequest request)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CreateExpense", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            cmd.Parameters.AddWithValue("@StatusId", request.StatusId);
            cmd.Parameters.AddWithValue("@AmountMinor", request.AmountMinor);
            cmd.Parameters.AddWithValue("@Currency", request.Currency);
            cmd.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            cmd.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SubmittedAt", (object?)request.SubmittedAt ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return (Convert.ToInt32(result), null);
        }
        catch (Exception ex)
        {
            return (null, BuildError(ex));
        }
    }

    public async Task<(bool Success, ServiceError? Error)> UpdateExpenseStatusAsync(
        int expenseId, ExpenseStatusUpdateRequest request)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_UpdateExpenseStatus", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@ExpenseId", expenseId);
            cmd.Parameters.AddWithValue("@StatusId", request.StatusId);
            cmd.Parameters.AddWithValue("@ReviewedBy", request.ReviewedBy);
            cmd.Parameters.AddWithValue("@ReviewedAt", DBNull.Value);

            var rows = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return (rows > 0, null);
        }
        catch (Exception ex)
        {
            return (false, BuildError(ex));
        }
    }

    // -------------------------------------------------
    // USERS
    // -------------------------------------------------

    public async Task<(List<User> Users, ServiceError? Error)> GetUsersAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetUsers", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            var users = new List<User>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                users.Add(MapUser(reader));
            return (users, null);
        }
        catch (Exception ex)
        {
            return (GetDummyUsers(), BuildError(ex));
        }
    }

    public async Task<(User? User, ServiceError? Error)> GetUserByIdAsync(int userId)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetUserById", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserId", userId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (MapUser(reader), null);
            return (null, null);
        }
        catch (Exception ex)
        {
            return (null, BuildError(ex));
        }
    }

    public async Task<(int? UserId, ServiceError? Error)> CreateUserAsync(UserCreateRequest request)
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_CreateUser", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@UserName", request.UserName);
            cmd.Parameters.AddWithValue("@Email", request.Email);
            cmd.Parameters.AddWithValue("@RoleId", request.RoleId);
            cmd.Parameters.AddWithValue("@ManagerId", (object?)request.ManagerId ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return (Convert.ToInt32(result), null);
        }
        catch (Exception ex)
        {
            return (null, BuildError(ex));
        }
    }

    // -------------------------------------------------
    // LOOKUPS
    // -------------------------------------------------

    public async Task<(List<ExpenseCategory> Categories, ServiceError? Error)> GetCategoriesAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseCategories", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            var cats = new List<ExpenseCategory>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                cats.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            return (cats, null);
        }
        catch (Exception ex)
        {
            return (GetDummyCategories(), BuildError(ex));
        }
    }

    public async Task<(List<ExpenseStatus> Statuses, ServiceError? Error)> GetStatusesAsync()
    {
        try
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.usp_GetExpenseStatuses", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            var statuses = new List<ExpenseStatus>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            return (statuses, null);
        }
        catch (Exception ex)
        {
            return (GetDummyStatuses(), BuildError(ex));
        }
    }

    // -------------------------------------------------
    // MAPPING HELPERS
    // -------------------------------------------------

    private static Expense MapExpense(SqlDataReader r) => new()
    {
        ExpenseId = r.GetInt32(r.GetOrdinal("ExpenseId")),
        UserId = r.GetInt32(r.GetOrdinal("UserId")),
        UserName = r.GetString(r.GetOrdinal("UserName")),
        Email = r.GetString(r.GetOrdinal("Email")),
        CategoryId = r.GetInt32(r.GetOrdinal("CategoryId")),
        CategoryName = r.GetString(r.GetOrdinal("CategoryName")),
        StatusId = r.GetInt32(r.GetOrdinal("StatusId")),
        StatusName = r.GetString(r.GetOrdinal("StatusName")),
        AmountMinor = r.GetInt32(r.GetOrdinal("AmountMinor")),
        Currency = r.GetString(r.GetOrdinal("Currency")),
        ExpenseDate = r.GetDateTime(r.GetOrdinal("ExpenseDate")),
        Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
        ReceiptFile = r.IsDBNull(r.GetOrdinal("ReceiptFile")) ? null : r.GetString(r.GetOrdinal("ReceiptFile")),
        SubmittedAt = r.IsDBNull(r.GetOrdinal("SubmittedAt")) ? null : r.GetDateTime(r.GetOrdinal("SubmittedAt")),
        ReviewedBy = r.IsDBNull(r.GetOrdinal("ReviewedBy")) ? null : r.GetInt32(r.GetOrdinal("ReviewedBy")),
        ReviewedByName = r.IsDBNull(r.GetOrdinal("ReviewedByName")) ? null : r.GetString(r.GetOrdinal("ReviewedByName")),
        ReviewedAt = r.IsDBNull(r.GetOrdinal("ReviewedAt")) ? null : r.GetDateTime(r.GetOrdinal("ReviewedAt")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
    };

    private static User MapUser(SqlDataReader r) => new()
    {
        UserId = r.GetInt32(r.GetOrdinal("UserId")),
        UserName = r.GetString(r.GetOrdinal("UserName")),
        Email = r.GetString(r.GetOrdinal("Email")),
        RoleId = r.GetInt32(r.GetOrdinal("RoleId")),
        RoleName = r.GetString(r.GetOrdinal("RoleName")),
        ManagerId = r.IsDBNull(r.GetOrdinal("ManagerId")) ? null : r.GetInt32(r.GetOrdinal("ManagerId")),
        ManagerName = r.IsDBNull(r.GetOrdinal("ManagerName")) ? null : r.GetString(r.GetOrdinal("ManagerName")),
        IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
        CreatedAt = r.GetDateTime(r.GetOrdinal("CreatedAt"))
    };

    // -------------------------------------------------
    // DUMMY / FALLBACK DATA
    // -------------------------------------------------

    private static List<Expense> GetDummyExpenses() => new()
    {
        new Expense { ExpenseId = 1, UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, Currency = "GBP", ExpenseDate = DateTime.UtcNow.AddDays(-7), Description = "Taxi from airport to client site (DEMO DATA)", CreatedAt = DateTime.UtcNow },
        new Expense { ExpenseId = 2, UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 1425, Currency = "GBP", ExpenseDate = DateTime.UtcNow.AddDays(-14), Description = "Client lunch meeting (DEMO DATA)", ReviewedByName = "Bob Manager", ReviewedAt = DateTime.UtcNow.AddDays(-13), CreatedAt = DateTime.UtcNow },
        new Expense { ExpenseId = 3, UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", AmountMinor = 799, Currency = "GBP", ExpenseDate = DateTime.UtcNow.AddDays(-1), Description = "Office stationery (DEMO DATA)", CreatedAt = DateTime.UtcNow }
    };

    private static List<User> GetDummyUsers() => new()
    {
        new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.UtcNow },
        new User { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.UtcNow }
    };

    private static List<ExpenseCategory> GetDummyCategories() => new()
    {
        new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
        new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
        new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
    };

    private static List<ExpenseStatus> GetDummyStatuses() => new()
    {
        new ExpenseStatus { StatusId = 1, StatusName = "Draft" },
        new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
        new ExpenseStatus { StatusId = 3, StatusName = "Approved" },
        new ExpenseStatus { StatusId = 4, StatusName = "Rejected" }
    };
}
