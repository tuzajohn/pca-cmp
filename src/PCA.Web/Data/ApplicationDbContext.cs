using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PCA.Modules.AccessManagement.Data;
using PCA.Modules.AccessManagement.Models;
using PCA.Modules.AccessManagement.Services;
using PCA.Modules.Invoicing.Data;
using PCA.Modules.Invoicing.Models;
using PCA.Modules.Invoicing.Services;
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
    IApplicationDbContextForIncidents,
    IApplicationDbContextForAccessManagement,
    IApplicationDbContextForInvoicing
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // ChangeManagement
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ChangeRequestComment> ChangeRequestComments => Set<ChangeRequestComment>();
    public DbSet<ChangeRequestSequence> ChangeRequestSequences => Set<ChangeRequestSequence>();

    // Approvals
    public DbSet<ApprovalTemplate> ApprovalTemplates => Set<ApprovalTemplate>();
    public DbSet<ApprovalTemplateStep> ApprovalTemplateSteps => Set<ApprovalTemplateStep>();
    public DbSet<ApprovalFlow> ApprovalFlows => Set<ApprovalFlow>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();

    // Documents
    public DbSet<DocumentFolder> DocumentFolders => Set<DocumentFolder>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<FolderPermission> FolderPermissions => Set<FolderPermission>();
    public DbSet<DocumentPermission> DocumentPermissions => Set<DocumentPermission>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<DocumentReview> DocumentReviews => Set<DocumentReview>();

    // Incidents
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentUpdate> IncidentUpdates => Set<IncidentUpdate>();
    public DbSet<IncidentDocument> IncidentDocuments => Set<IncidentDocument>();
    public DbSet<IncidentSequence> IncidentSequences => Set<IncidentSequence>();

    // Access Management
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<AccessRequestComment> AccessRequestComments => Set<AccessRequestComment>();
    public DbSet<AccessReview> AccessReviews => Set<AccessReview>();
    public DbSet<AccessReviewEntry> AccessReviewEntries => Set<AccessReviewEntry>();
    public DbSet<DeprovisioningEvent> DeprovisioningEvents => Set<DeprovisioningEvent>();
    public DbSet<DeprovisioningSystemEntry> DeprovisioningSystemEntries => Set<DeprovisioningSystemEntry>();
    public DbSet<ServerRoomAccessRequest> ServerRoomAccessRequests => Set<ServerRoomAccessRequest>();
    public DbSet<ServerRoomAccessComment> ServerRoomAccessComments => Set<ServerRoomAccessComment>();
    public DbSet<AccessSequence> AccessSequences => Set<AccessSequence>();

    // Invoicing
    public DbSet<InvoiceLender> InvoiceLenders => Set<InvoiceLender>();
    public DbSet<InvoiceRecipient> InvoiceRecipients => Set<InvoiceRecipient>();
    public DbSet<InvoiceSchedule> InvoiceSchedules => Set<InvoiceSchedule>();
    public DbSet<InvoiceScheduleRecipient> InvoiceScheduleRecipients => Set<InvoiceScheduleRecipient>();
    public DbSet<InvoiceRun> InvoiceRuns => Set<InvoiceRun>();
    public DbSet<InvoiceHcmRefFile> InvoiceHcmRefFiles => Set<InvoiceHcmRefFile>();
    public DbSet<HcmMapping> HcmMappings => Set<HcmMapping>();

    // Attachments
    public DbSet<Attachment> Attachments => Set<Attachment>();

    // Logs
    public DbSet<AppLog> AppLogs => Set<AppLog>();

    // Theme
    public DbSet<ThemeSettings> ThemeSettings => Set<ThemeSettings>();

    // API Keys
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ChangeRequestConfiguration());
        builder.ApplyConfiguration(new ChangeRequestCommentConfiguration());
        builder.ApplyConfiguration(new ChangeRequestSequenceConfiguration());
        builder.ApplyConfiguration(new ApprovalTemplateConfiguration());
        builder.ApplyConfiguration(new ApprovalTemplateStepConfiguration());
        builder.ApplyConfiguration(new ApprovalFlowConfiguration());
        builder.ApplyConfiguration(new ApprovalStepConfiguration());
        builder.ApplyConfiguration(new DocumentReviewConfiguration());
        builder.ApplyConfiguration(new DocumentFolderConfiguration());
        builder.ApplyConfiguration(new DocumentConfiguration());
        builder.ApplyConfiguration(new DocumentVersionConfiguration());
        builder.ApplyConfiguration(new FolderPermissionConfiguration());
        builder.ApplyConfiguration(new DocumentPermissionConfiguration());
        builder.ApplyConfiguration(new IncidentConfiguration());
        builder.ApplyConfiguration(new IncidentUpdateConfiguration());
        builder.ApplyConfiguration(new IncidentDocumentConfiguration());

        builder.ApplyConfiguration(new AccessRequestConfiguration());
        builder.ApplyConfiguration(new AccessRequestCommentConfiguration());
        builder.ApplyConfiguration(new AccessReviewConfiguration());
        builder.ApplyConfiguration(new AccessReviewEntryConfiguration());
        builder.ApplyConfiguration(new DeprovisioningEventConfiguration());
        builder.ApplyConfiguration(new DeprovisioningSystemEntryConfiguration());
        builder.ApplyConfiguration(new ServerRoomAccessRequestConfiguration());
        builder.ApplyConfiguration(new ServerRoomAccessCommentConfiguration());

        builder.ApplyConfiguration(new InvoiceLenderConfiguration());
        builder.ApplyConfiguration(new InvoiceRecipientConfiguration());
        builder.ApplyConfiguration(new InvoiceScheduleConfiguration());
        builder.ApplyConfiguration(new InvoiceScheduleRecipientConfiguration());
        builder.ApplyConfiguration(new InvoiceRunConfiguration());
        builder.ApplyConfiguration(new InvoiceHcmRefFileConfiguration());

        builder.Entity<AccessSequence>().ToTable("AccessSequences").HasKey(x => x.Id);
        builder.Entity<AccessSequence>().HasIndex(x => new { x.Prefix, x.Year, x.Month }).IsUnique();

        builder.Entity<DocumentSequence>().ToTable("DocumentSequences").HasKey(x => x.Id);
        builder.Entity<IncidentSequence>().ToTable("IncidentSequences").HasKey(x => x.Id);
        builder.Entity<ThemeSettings>().ToTable("ThemeSettings").HasKey(x => x.Id);

        builder.Entity<AppLog>(b =>
        {
            b.ToTable("AppLogs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Level).HasMaxLength(20).IsRequired();
            b.Property(x => x.Category).HasMaxLength(50).IsRequired();
            b.Property(x => x.Source).HasMaxLength(200).IsRequired();
            b.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            b.Property(x => x.Details).HasColumnType("text");
            b.Property(x => x.Action).HasMaxLength(200);
            b.Property(x => x.EntityType).HasMaxLength(100);
            b.Property(x => x.UserEmail).HasMaxLength(300);
            b.Property(x => x.IpAddress).HasMaxLength(45);
            b.HasIndex(x => x.Timestamp);
            b.HasIndex(x => new { x.Source, x.Level });
        });

        builder.Entity<ApiKey>(b =>
        {
            b.ToTable("ApiKeys");
            b.HasKey(x => x.Id);
            b.Property(x => x.AppName).HasMaxLength(200).IsRequired();
            b.Property(x => x.KeyHash).HasMaxLength(64).IsRequired();
            b.Property(x => x.KeyPrefix).HasMaxLength(16).IsRequired();
            b.HasIndex(x => x.KeyHash).IsUnique();
        });

        builder.Entity<Attachment>(b =>
        {
            b.ToTable("Attachments");
            b.HasKey(x => x.Id);
            b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            b.Property(x => x.OriginalFileName).HasMaxLength(500).IsRequired();
            b.Property(x => x.StoredFileName).HasMaxLength(500).IsRequired();
            b.Property(x => x.FilePath).HasMaxLength(1000).IsRequired();
            b.Property(x => x.ContentType).HasMaxLength(200);
            b.HasIndex(x => new { x.EntityType, x.EntityId });
            b.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
