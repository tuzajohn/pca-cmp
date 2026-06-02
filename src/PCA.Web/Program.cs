using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PCA.Modules.Approvals;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Identity;
using PCA.Modules.Identity.Models;
using PCA.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Register ApplicationDbContext as the module interfaces
builder.Services.AddScoped<IApplicationDbContextForCM>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IApplicationDbContextForApprovals>(sp => sp.GetRequiredService<ApplicationDbContext>());

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Register modules
var config = builder.Configuration;
new PCA.Modules.Identity.ModuleRegistration().Register(builder.Services, config);
new PCA.Modules.ChangeManagement.ModuleRegistration().Register(builder.Services, config);
new PCA.Modules.Approvals.ModuleRegistration().Register(builder.Services, config);

// MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed database
await PCA.Web.Data.DbSeeder.SeedAsync(app.Services);

app.Run();
