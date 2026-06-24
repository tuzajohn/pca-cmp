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

        // HcmMappings table — managed outside EF migrations
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS HcmMappings (
                Id             INT          NOT NULL AUTO_INCREMENT,
                RawValue       VARCHAR(500) NOT NULL,
                CanonicalName  VARCHAR(200) NULL,
                Classification VARCHAR(50)  NOT NULL,
                SourceColumn   VARCHAR(50)  NOT NULL,
                Aliases        TEXT         NULL,
                CreatedAt      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                PRIMARY KEY (Id),
                UNIQUE KEY UQ_HcmMapping (RawValue(200), SourceColumn)
            ) CHARACTER SET utf8mb4");

        // Ensure invoice storage folders exist
        var storageRoot = config["InvoiceStoragePath"]
            ?? Path.Combine(env.ContentRootPath, "uploads", "documents");
        Directory.CreateDirectory(Path.Combine(storageRoot, "invoices", "hcm-ref"));

        // Create year/month folders for any existing invoice runs and migrate
        // file paths from old flat layout (invoices/yyyy-MM/) to year/month (invoices/yyyy/MM/)
        var existingRuns = await db.InvoiceRuns
            .Where(r => r.FilePath != null && r.FilePath != "")
            .ToListAsync();
        bool runsMigrated = false;
        foreach (var run in existingRuns)
        {
            var newDir = Path.Combine(storageRoot, "invoices",
                run.TriggeredAt.Year.ToString(), run.TriggeredAt.Month.ToString("D2"));
            Directory.CreateDirectory(newDir);

            if (run.FilePath == null) continue;
            var fileName = Path.GetFileName(run.FilePath);
            var newPath  = Path.Combine(newDir, fileName);

            if (run.FilePath != newPath)
            {
                if (System.IO.File.Exists(run.FilePath) && !System.IO.File.Exists(newPath))
                    System.IO.File.Move(run.FilePath, newPath);
                run.FilePath = newPath;
                runsMigrated = true;
            }
        }
        if (runsMigrated) await db.SaveChangesAsync();

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

        // Seed "Invoices" document folder
        if (!await db.DocumentFolders.AnyAsync(f => f.Name == "Invoices" && f.ParentId == null))
        {
            db.DocumentFolders.Add(new PCA.Modules.Documents.Models.DocumentFolder
            {
                Name        = "Invoices",
                Description = "Auto-generated invoice files",
                CreatedById = admin!.Id
            });
            await db.SaveChangesAsync();
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
