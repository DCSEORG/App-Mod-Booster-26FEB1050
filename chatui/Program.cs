using ExpenseChat.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Register chat service
builder.Services.AddScoped<IChatService, ChatService>();

// HTTP client for Expense API
var expenseApiBase = builder.Configuration["ExpenseApiBaseUrl"] ?? "http://localhost:5000";
builder.Services.AddHttpClient("ExpenseApi", client =>
{
    client.BaseAddress = new Uri(expenseApiBase);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/chat"));

app.Run();
