using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PCA.Modules.AccessManagement;
using PCA.Modules.AccessManagement.Services;
using PCA.Modules.Invoicing;
using PCA.Modules.Invoicing.Services;
using PCA.Web.Services;
using PCA.Modules.Approvals;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Documents.Services;
using PCA.Modules.Identity;
using PCA.Modules.Identity.Models;
using PCA.Modules.Incidents;
using PCA.Modules.Incidents.Services;
using PCA.Web.Data;
using PCA.Web.Services;
using PCA.Web.Workflows;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var mySqlVersion = builder.Configuration["MySqlVersion"] ?? "8.0.33-mysql";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.Parse(mySqlVersion)));

// Register ApplicationDbContext as the module interfaces
builder.Services.AddScoped<IApplicationDbContextForCM>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IApplicationDbContextForApprovals>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IApplicationDbContextForDocuments>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IApplicationDbContextForIncidents>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IApplicationDbContextForAccessManagement>(sp => sp.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IApplicationDbContextForInvoicing>(sp => sp.GetRequiredService<ApplicationDbContext>());

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
new PCA.Modules.Incidents.ModuleRegistration().Register(builder.Services, config);
new PCA.Modules.AccessManagement.ModuleRegistration().Register(builder.Services, config);
new PCA.Modules.Invoicing.ModuleRegistration().Register(builder.Services, config);

// Document storage
var docsStorageRoot = builder.Configuration["DocumentStoragePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "uploads", "documents");
builder.Services.AddScoped<IDocumentService>(sp =>
    new DocumentService(sp.GetRequiredService<IApplicationDbContextForDocuments>(), docsStorageRoot));

// Attachment storage
var attachmentStorageRoot = builder.Configuration["AttachmentStoragePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "uploads", "attachments");
builder.Services.AddScoped<IAttachmentService>(sp =>
    new AttachmentService(sp.GetRequiredService<ApplicationDbContext>(), attachmentStorageRoot));

// Email
var smtpSettings = builder.Configuration.GetSection("Smtp").Get<SmtpSettings>() ?? new SmtpSettings();
builder.Services.AddSingleton(smtpSettings);
builder.Services.AddScoped<IEmailService, EmailService>();

// Theme
builder.Services.AddScoped<IThemeService, ThemeService>();

// Approval workflow registry
builder.Services.AddSingleton<IApprovalWorkflowRegistry>(_ =>
{
    var registry = new ApprovalWorkflowRegistry();
    registry.Register(new ChangeRequestApprovalWorkflow());
    registry.Register(new IncidentApprovalWorkflow());
    registry.Register(new DocumentApprovalWorkflow());
    registry.Register(new AccessRequestApprovalWorkflow());
    registry.Register(new ServerRoomAccessApprovalWorkflow());
    return registry;
});

// Document review alert background worker
builder.Services.AddHostedService<DocumentReviewAlertWorker>();
// Deprovisioning SLA alert background worker
builder.Services.AddHostedService<DeprovisioningAlertWorker>();

// Invoice scheduler background worker
builder.Services.AddScoped<IInvoiceEmailSender, InvoiceEmailSender>();
var invoiceStorageRoot = builder.Configuration["InvoiceStoragePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "uploads", "documents");
builder.Services.AddScoped<InvoiceRunOrchestrator>(sp => new InvoiceRunOrchestrator(
    sp.GetRequiredService<IInvoicingService>(),
    sp.GetRequiredService<InvoiceDataService>(),
    sp.GetRequiredService<IInvoiceEmailSender>(),
    invoiceStorageRoot,
    sp.GetRequiredService<ILogger<InvoiceRunOrchestrator>>()));
builder.Services.AddHostedService<InvoiceSchedulerWorker>();

// Logging
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Logging.AddProvider(new DbLoggerProvider(
    builder.Services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>()));

// MVC + API controllers
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<PCA.Web.Filters.DeprovisioningOverdueBadgeFilter>();
});
builder.Services.AddControllers();

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

app.MapControllers(); // API routes (api/logs, etc.)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed database
await PCA.Web.Data.DbSeeder.SeedAsync(app.Services);

app.Run();
