using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PCA.Modules.Approvals.Data;
using PCA.Modules.Approvals.Models;
using PCA.Modules.Approvals.Services;
using PCA.Modules.ChangeManagement.Data;
using PCA.Modules.ChangeManagement.Models;
using PCA.Modules.ChangeManagement.Services;
using PCA.Modules.Documents.Data;
using PCA.Modules.Documents.Models;
using PCA.Modules.Documents.Services;
using PCA.Modules.Incidents.Data;
using PCA.Modules.Incidents.Models;
using PCA.Modules.Incidents.Services;
using PCA.Modules.Identity.Models;
using PCA.Web.Models;

namespace PCA.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>,
    IApplicationDbContextForCM,
    IApplicationDbContextForApprovals,
    IApplicationDbContextForDocuments,
    IApplicationDbContextForIncidents
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

    // Documents
    public DbSet<DocumentFolder> DocumentFolders => Set<DocumentFolder>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<FolderPermission> FolderPermissions => Set<FolderPermission>();
    public DbSet<DocumentPermission> DocumentPermissions => Set<DocumentPermission>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    // Incidents
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentUpdate> IncidentUpdates => Set<IncidentUpdate>();
    public DbSet<IncidentDocument> IncidentDocuments => Set<IncidentDocument>();
    public DbSet<IncidentSequence> IncidentSequences => Set<IncidentSequence>();

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
        builder.ApplyConfiguration(new DocumentFolderConfiguration());
        builder.ApplyConfiguration(new DocumentConfiguration());
        builder.ApplyConfiguration(new DocumentVersionConfiguration());
        builder.ApplyConfiguration(new FolderPermissionConfiguration());
        builder.ApplyConfiguration(new DocumentPermissionConfiguration());
        builder.ApplyConfiguration(new IncidentConfiguration());
        builder.ApplyConfiguration(new IncidentUpdateConfiguration());
        builder.ApplyConfiguration(new IncidentDocumentConfiguration());

        builder.Entity<DocumentSequence>().ToTable("DocumentSequences").HasKey(x => x.Id);
        builder.Entity<IncidentSequence>().ToTable("IncidentSequences").HasKey(x => x.Id);
        builder.Entity<ThemeSettings>().ToTable("ThemeSettings").HasKey(x => x.Id);
    }
}
