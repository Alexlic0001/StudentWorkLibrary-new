using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentWorkLibrary;
using StudentWorkLibrary.Components;
using StudentWorkLibrary.Data;
using StudentWorkLibrary.Services;

var builder = WebApplication.CreateBuilder(args);

// SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=StudentWorkLibrary.db";

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Blazor Web App
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// Свои сервисы
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IWorkSearchService, WorkSearchService>();

// Razor Pages (Login/Logout) + Controllers (Download)
builder.Services.AddRazorPages();
builder.Services.AddControllers();

var app = builder.Build();

// Seed
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedData.InitializeAsync(services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Antiforgery нужен для Blazor Web App
app.UseAntiforgery();

app.MapRazorPages();
app.MapControllers();
app.MapGet("/api/test", () => "API works");
// Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();