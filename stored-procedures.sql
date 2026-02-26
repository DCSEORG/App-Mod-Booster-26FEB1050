-- stored-procedures.sql
-- Stored procedures for the Expense Management System
-- Run this against the 'expenses' database after the schema has been imported

-- ============================================================
-- usp_GetExpenses
-- Returns all expenses with optional filters
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenses
    @UserId     INT = NULL,
    @StatusId   INT = NULL,
    @CategoryId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rb.UserName  AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users            u  ON e.UserId     = u.UserId
    JOIN dbo.ExpenseCategories c  ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus     s  ON e.StatusId   = s.StatusId
    LEFT JOIN dbo.Users        rb ON e.ReviewedBy = rb.UserId
    WHERE
        (@UserId     IS NULL OR e.UserId     = @UserId)
        AND (@StatusId   IS NULL OR e.StatusId   = @StatusId)
        AND (@CategoryId IS NULL OR e.CategoryId = @CategoryId)
    ORDER BY e.CreatedAt DESC;
END
GO

-- ============================================================
-- usp_GetExpenseById
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        rb.UserName  AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    JOIN dbo.Users            u  ON e.UserId     = u.UserId
    JOIN dbo.ExpenseCategories c  ON e.CategoryId = c.CategoryId
    JOIN dbo.ExpenseStatus     s  ON e.StatusId   = s.StatusId
    LEFT JOIN dbo.Users        rb ON e.ReviewedBy = rb.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- ============================================================
-- usp_CreateExpense
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId      INT,
    @CategoryId  INT,
    @StatusId    INT,
    @AmountMinor INT,
    @Currency    NVARCHAR(3)   = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500)  = NULL,
    @SubmittedAt DATETIME2      = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Expenses
        (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, SubmittedAt, CreatedAt)
    VALUES
        (@UserId, @CategoryId, @StatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, @SubmittedAt, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS ExpenseId;
END
GO

-- ============================================================
-- usp_UpdateExpenseStatus
-- Approve or reject an expense
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_UpdateExpenseStatus
    @ExpenseId   INT,
    @StatusId    INT,
    @ReviewedBy  INT,
    @ReviewedAt  DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Expenses
    SET
        StatusId   = @StatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = ISNULL(@ReviewedAt, SYSUTCDATETIME())
    WHERE ExpenseId = @ExpenseId;

    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- ============================================================
-- usp_GetUsers
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetUsers
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m ON u.ManagerId = m.UserId
    ORDER BY u.UserName;
END
GO

-- ============================================================
-- usp_GetUserById
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetUserById
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m ON u.ManagerId = m.UserId
    WHERE u.UserId = @UserId;
END
GO

-- ============================================================
-- usp_CreateUser
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateUser
    @UserName  NVARCHAR(100),
    @Email     NVARCHAR(255),
    @RoleId    INT,
    @ManagerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Users (UserName, Email, RoleId, ManagerId, IsActive, CreatedAt)
    VALUES (@UserName, @Email, @RoleId, @ManagerId, 1, SYSUTCDATETIME());

    SELECT SCOPE_IDENTITY() AS UserId;
END
GO

-- ============================================================
-- usp_GetExpenseCategories
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseCategories
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, CategoryName, IsActive FROM dbo.ExpenseCategories ORDER BY CategoryName;
END
GO

-- ============================================================
-- usp_GetExpenseStatuses
-- ============================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseStatuses
AS
BEGIN
    SET NOCOUNT ON;
    SELECT StatusId, StatusName FROM dbo.ExpenseStatus ORDER BY StatusId;
END
GO
