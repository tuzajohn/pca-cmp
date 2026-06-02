using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PCA.Modules.ChangeManagement.Models;

namespace PCA.Modules.ChangeManagement.Data;

public class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("ChangeRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SerialNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.SerialNumber).IsUnique();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ImplementationNotes).HasMaxLength(4000);
        builder.Property(x => x.SystemsAffected).HasMaxLength(1000);
        builder.Property(x => x.RiskDescription).HasMaxLength(2000);
        builder.Property(x => x.ImpactOnUsers).HasMaxLength(1000);
        builder.Property(x => x.ProposedImplementationWindow).HasMaxLength(500);
        builder.Property(x => x.RollbackPlan).HasMaxLength(4000);
        builder.Property(x => x.RollbackTrigger).HasMaxLength(1000);
        builder.Property(x => x.TestingSteps).HasMaxLength(4000);
        builder.Property(x => x.StagingTested).HasConversion<string>();
        builder.Property(x => x.Type).HasConversion<string>();
        builder.Property(x => x.Priority).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.HasOne(x => x.RequestedBy)
            .WithMany()
            .HasForeignKey(x => x.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Comments)
            .WithOne(x => x.ChangeRequest)
            .HasForeignKey(x => x.ChangeRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChangeRequestCommentConfiguration : IEntityTypeConfiguration<ChangeRequestComment>
{
    public void Configure(EntityTypeBuilder<ChangeRequestComment> builder)
    {
        builder.ToTable("ChangeRequestComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        builder.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChangeRequestSequenceConfiguration : IEntityTypeConfiguration<ChangeRequestSequence>
{
    public void Configure(EntityTypeBuilder<ChangeRequestSequence> builder)
    {
        builder.ToTable("ChangeRequestSequences");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Year, x.Month }).IsUnique();
    }
}
