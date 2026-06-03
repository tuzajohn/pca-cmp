using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PCA.Modules.Incidents.Models;

namespace PCA.Modules.Incidents.Data;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incidents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SerialNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.AffectedSystems).HasColumnType("text");
        builder.Property(x => x.ImpactDescription).HasColumnType("text");
        builder.Property(x => x.RootCause).HasColumnType("text");
        builder.Property(x => x.ResolutionSummary).HasColumnType("text");
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.ReportedBy)
            .WithMany()
            .HasForeignKey(x => x.ReportedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedTo)
            .WithMany()
            .HasForeignKey(x => x.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Updates)
            .WithOne(x => x.Incident)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.LinkedDocuments)
            .WithOne(x => x.Incident)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class IncidentUpdateConfiguration : IEntityTypeConfiguration<IncidentUpdate>
{
    public void Configure(EntityTypeBuilder<IncidentUpdate> builder)
    {
        builder.ToTable("IncidentUpdates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Content).HasColumnType("text").IsRequired();
        builder.Property(x => x.UpdateType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Author)
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IncidentDocumentConfiguration : IEntityTypeConfiguration<IncidentDocument>
{
    public void Configure(EntityTypeBuilder<IncidentDocument> builder)
    {
        builder.ToTable("IncidentDocuments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.IncidentId, x.DocumentId }).IsUnique();

        builder.HasOne(x => x.LinkedBy)
            .WithMany()
            .HasForeignKey(x => x.LinkedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
