using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PCA.Modules.AccessManagement.Models;
using PCA.Shared.Enums;

namespace PCA.Modules.AccessManagement.Data;

public class AccessRequestConfiguration : IEntityTypeConfiguration<AccessRequest>
{
    public void Configure(EntityTypeBuilder<AccessRequest> builder)
    {
        builder.ToTable("AccessRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SerialNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.SerialNumber).IsUnique();
        builder.Property(x => x.RequestedById).HasMaxLength(450).IsRequired();
        builder.Property(x => x.EmployeeName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.EmployeeId).HasMaxLength(100);
        builder.Property(x => x.Department).HasMaxLength(200);
        builder.Property(x => x.JobTitle).HasMaxLength(200);
        builder.Property(x => x.SystemName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.AccessType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.AccessDetails).HasColumnType("text").IsRequired();
        builder.Property(x => x.Justification).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ProvisionedById).HasMaxLength(450);
        builder.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProvisionedBy).WithMany().HasForeignKey(x => x.ProvisionedById)
            .OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        builder.HasMany(x => x.Comments).WithOne(x => x.AccessRequest)
            .HasForeignKey(x => x.AccessRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccessRequestCommentConfiguration : IEntityTypeConfiguration<AccessRequestComment>
{
    public void Configure(EntityTypeBuilder<AccessRequestComment> builder)
    {
        builder.ToTable("AccessRequestComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AuthorId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Content).HasColumnType("text").IsRequired();
        builder.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccessReviewConfiguration : IEntityTypeConfiguration<AccessReview>
{
    public void Configure(EntityTypeBuilder<AccessReview> builder)
    {
        builder.ToTable("AccessReviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Cycle).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.CreatedById).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Entries).WithOne(x => x.AccessReview)
            .HasForeignKey(x => x.AccessReviewId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AccessReviewEntryConfiguration : IEntityTypeConfiguration<AccessReviewEntry>
{
    public void Configure(EntityTypeBuilder<AccessReviewEntry> builder)
    {
        builder.ToTable("AccessReviewEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EmployeeName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Department).HasMaxLength(200);
        builder.Property(x => x.SystemName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.CurrentAccessLevel).HasMaxLength(300);
        builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ReviewerNotes).HasColumnType("text");
        builder.Property(x => x.ReviewedById).HasMaxLength(450);
        builder.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull).IsRequired(false);
    }
}

public class DeprovisioningEventConfiguration : IEntityTypeConfiguration<DeprovisioningEvent>
{
    public void Configure(EntityTypeBuilder<DeprovisioningEvent> builder)
    {
        builder.ToTable("DeprovisioningEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SerialNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.SerialNumber).IsUnique();
        builder.Property(x => x.EmployeeName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.EmployeeId).HasMaxLength(100);
        builder.Property(x => x.Department).HasMaxLength(200);
        builder.Property(x => x.JobTitle).HasMaxLength(200);
        builder.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.TriggerDetails).HasColumnType("text");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.NotifiedById).HasMaxLength(450).IsRequired();
        builder.Property(x => x.CompletedById).HasMaxLength(450);
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.HasOne(x => x.NotifiedBy).WithMany().HasForeignKey(x => x.NotifiedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CompletedBy).WithMany().HasForeignKey(x => x.CompletedById)
            .OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        builder.HasMany(x => x.SystemEntries).WithOne(x => x.DeprovisioningEvent)
            .HasForeignKey(x => x.DeprovisioningEventId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeprovisioningSystemEntryConfiguration : IEntityTypeConfiguration<DeprovisioningSystemEntry>
{
    public void Configure(EntityTypeBuilder<DeprovisioningSystemEntry> builder)
    {
        builder.ToTable("DeprovisioningSystemEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SystemName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.AccessDescription).HasColumnType("text");
        builder.Property(x => x.DeactivatedById).HasMaxLength(450);
        builder.HasOne(x => x.DeactivatedBy).WithMany().HasForeignKey(x => x.DeactivatedById)
            .OnDelete(DeleteBehavior.SetNull).IsRequired(false);
    }
}

public class ServerRoomAccessRequestConfiguration : IEntityTypeConfiguration<ServerRoomAccessRequest>
{
    public void Configure(EntityTypeBuilder<ServerRoomAccessRequest> builder)
    {
        builder.ToTable("ServerRoomAccessRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SerialNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.SerialNumber).IsUnique();
        builder.Property(x => x.RequestedById).HasMaxLength(450).IsRequired();
        builder.Property(x => x.VisitorName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.VisitorTitle).HasMaxLength(200);
        builder.Property(x => x.VisitorCompany).HasMaxLength(300);
        builder.Property(x => x.Purpose).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.WrittenRequestReference).HasMaxLength(500);
        builder.Property(x => x.EscortedBy).HasMaxLength(300);
        builder.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Comments).WithOne(x => x.ServerRoomAccessRequest)
            .HasForeignKey(x => x.ServerRoomAccessRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ServerRoomAccessCommentConfiguration : IEntityTypeConfiguration<ServerRoomAccessComment>
{
    public void Configure(EntityTypeBuilder<ServerRoomAccessComment> builder)
    {
        builder.ToTable("ServerRoomAccessComments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AuthorId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Content).HasColumnType("text").IsRequired();
        builder.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
