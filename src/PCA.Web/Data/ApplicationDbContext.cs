using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PCA.Modules.Approvals.Data;
using PCA.Modules.Approvals.Models;
using PCA.Modules.ChangeManagement.Data;
using PCA.Modules.ChangeManagement.Models;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Approvals.Services;
using PCA.Modules.Identity.Models;
using PCA.Web.Models;

namespace PCA.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>,
    IApplicationDbContextForCM,
    IApplicationDbContextForApprovals
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // ChangeManagement
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ChangeRequestComment> ChangeRequestComments => Set<ChangeRequestComment>();
    public DbSet<ChangeRequestSequence> ChangeRequestSequences => Set<ChangeRequestSequence>();

    // Approvals
    public DbSet<ApprovalTemplate> ApprovalTemplates => Set<ApprovalTemplate>();
    public DbSet<ApprovalTemplateStep> ApprovalTemplateSteps => Set<ApprovalTemplateStep>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();

    // Theme
    public DbSet<ThemeSettings> ThemeSettings => Set<ThemeSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ChangeRequestConfiguration());
        builder.ApplyConfiguration(new ChangeRequestCommentConfiguration());
        builder.ApplyConfiguration(new ChangeRequestSequenceConfiguration());
        builder.ApplyConfiguration(new ApprovalTemplateConfiguration());
        builder.ApplyConfiguration(new ApprovalTemplateStepConfiguration());
        builder.ApplyConfiguration(new ApprovalStepConfiguration());

        builder.Entity<ThemeSettings>().ToTable("ThemeSettings").HasKey(x => x.Id);
    }
}
