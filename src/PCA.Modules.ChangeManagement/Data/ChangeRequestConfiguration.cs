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

        // Large text fields → MySQL TEXT (stored off-row, not counted in the 65535-byte row limit)
        builder.Property(x => x.Description).HasColumnType("text").IsRequired();
        builder.Property(x => x.ImplementationNotes).HasColumnType("text");
        builder.Property(x => x.RiskDescription).HasColumnType("text");
        builder.Property(x => x.RollbackPlan).HasColumnType("text");
        builder.Property(x => x.TestingSteps).HasColumnType("text");

        // Shorter fields stay as varchar
        builder.Property(x => x.SystemsAffected).HasMaxLength(1000);
        builder.Property(x => x.ImpactOnUsers).HasMaxLength(1000);
        builder.Property(x => x.ProposedImplementationWindow).HasMaxLength(500);
        builder.Property(x => x.RollbackTrigger).HasMaxLength(1000);

        // Enum string conversions (stored as varchar — short values, fine inline)
        builder.Property(x => x.StagingTested).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        // PIR fields
        builder.Property(x => x.PirOutcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PirIssuesEncountered).HasColumnType("text");
        builder.Property(x => x.PirLessonsLearned).HasColumnType("text");
        builder.Property(x => x.PirClosureNotes).HasColumnType("text");
        builder.HasOne(x => x.PirCompletedBy)
            .WithMany()
            .HasForeignKey(x => x.PirCompletedById)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

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
        builder.Property(x => x.Content).HasColumnType("text").IsRequired();
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
