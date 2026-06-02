using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PCA.Modules.Approvals.Models;
using PCA.Modules.Identity.Models;
using PCA.Shared.Enums;

namespace PCA.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        // Seed roles
        foreach (var role in new[] { "Admin", "Approver", "Requester" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed admin user
        var adminEmail = "admin@pca.local";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                Department = "IT",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRolesAsync(admin, new[] { "Admin", "Approver" });
            }
        }

        // Seed approval templates
        if (!await db.ApprovalTemplates.AnyAsync())
        {
            var changeTypes = new[] { ChangeType.Standard, ChangeType.Emergency, ChangeType.Normal };
            foreach (var ct in changeTypes)
            {
                var template = new ApprovalTemplate
                {
                    Name = $"{ct} Change Approval",
                    ChangeType = ct,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Steps = new List<ApprovalTemplateStep>
                    {
                        new ApprovalTemplateStep
                        {
                            Order = 1,
                            ApproverId = admin!.Id,
                            RoleName = "Approver",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new ApprovalTemplateStep
                        {
                            Order = 2,
                            ApproverId = admin!.Id,
                            RoleName = "Admin",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }
                    }
                };
                db.ApprovalTemplates.Add(template);
            }
            await db.SaveChangesAsync();
        }
    }
}
