using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PCA.Modules.Approvals.Models;
using PCA.Modules.Identity.Models;

namespace PCA.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var config      = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var env         = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        await db.Database.MigrateAsync();

        // Ensure invoice storage folders exist
        var storageRoot = config["InvoiceStoragePath"]
            ?? Path.Combine(env.ContentRootPath, "uploads", "documents");
        Directory.CreateDirectory(Path.Combine(storageRoot, "invoices", "hcm-ref"));

        // Create month folders for any existing invoice runs
        var runMonths = await db.InvoiceRuns
            .Where(r => r.TriggeredAt != default)
            .Select(r => r.TriggeredAt.ToString("yyyy-MM"))
            .Distinct()
            .ToListAsync();
        foreach (var month in runMonths)
            Directory.CreateDirectory(Path.Combine(storageRoot, "invoices", month));

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
                // Admin gets all module claims so existing installs work correctly
                foreach (var (key, _, _) in AppModules.All)
                    await userManager.AddClaimAsync(admin,
                        new System.Security.Claims.Claim(AppModules.ClaimType, key));
            }
        }

        // Seed approval templates
        if (!await db.ApprovalTemplates.AnyAsync())
        {
            var crSubTypes = new[] { "Standard", "Normal", "Emergency" };
            foreach (var subType in crSubTypes)
            {
                db.ApprovalTemplates.Add(new ApprovalTemplate
                {
                    Name = $"{subType} Change Approval",
                    EntityType = "ChangeRequest",
                    EntitySubType = subType,
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
                });
            }

            db.ApprovalTemplates.Add(new ApprovalTemplate
            {
                Name = "Incident Approval",
                EntityType = "Incident",
                EntitySubType = null,
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
                    }
                }
            });

            await db.SaveChangesAsync();
        }

        // Seed access request templates (Standard + Privileged) if missing
        if (!await db.ApprovalTemplates.AnyAsync(t => t.EntityType == "AccessRequest"))
        {
            db.ApprovalTemplates.Add(new ApprovalTemplate
            {
                Name = "Standard Access Approval",
                EntityType = "AccessRequest",
                EntitySubType = "Standard",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Steps = new List<ApprovalTemplateStep>
                {
                    new ApprovalTemplateStep { Order = 1, ApproverId = admin!.Id, RoleName = "Line Manager",    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new ApprovalTemplateStep { Order = 2, ApproverId = admin!.Id, RoleName = "General Manager", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                }
            });

            db.ApprovalTemplates.Add(new ApprovalTemplate
            {
                Name = "Privileged Access Approval",
                EntityType = "AccessRequest",
                EntitySubType = "Privileged",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Steps = new List<ApprovalTemplateStep>
                {
                    new ApprovalTemplateStep { Order = 1, ApproverId = admin!.Id, RoleName = "Line Manager",                     CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new ApprovalTemplateStep { Order = 2, ApproverId = admin!.Id, RoleName = "Senior Systems Administrator",     CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new ApprovalTemplateStep { Order = 3, ApproverId = admin!.Id, RoleName = "General Manager",                  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                }
            });

            await db.SaveChangesAsync();
        }

        // Seed deprovisioning notification template if missing
        if (!await db.ApprovalTemplates.AnyAsync(t => t.EntityType == "Deprovisioning"))
        {
            db.ApprovalTemplates.Add(new ApprovalTemplate
            {
                Name = "Deprovisioning Notification",
                EntityType = "Deprovisioning",
                EntitySubType = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Steps = new List<ApprovalTemplateStep>
                {
                    new ApprovalTemplateStep { Order = 1, ApproverId = admin!.Id, RoleName = "IT Manager",    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new ApprovalTemplateStep { Order = 2, ApproverId = admin!.Id, RoleName = "System Admin",  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                }
            });

            await db.SaveChangesAsync();
        }

        // Seed server room access template if missing
        if (!await db.ApprovalTemplates.AnyAsync(t => t.EntityType == "ServerRoomAccess"))
        {
            db.ApprovalTemplates.Add(new ApprovalTemplate
            {
                Name = "Server Room Access Approval",
                EntityType = "ServerRoomAccess",
                EntitySubType = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Steps = new List<ApprovalTemplateStep>
                {
                    new ApprovalTemplateStep { Order = 1, ApproverId = admin!.Id, RoleName = "IT Manager",       CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new ApprovalTemplateStep { Order = 2, ApproverId = admin!.Id, RoleName = "General Manager",  CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                }
            });

            await db.SaveChangesAsync();
        }
    }
}
