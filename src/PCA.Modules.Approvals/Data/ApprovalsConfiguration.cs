using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PCA.Modules.Approvals.Models;

namespace PCA.Modules.Approvals.Data;

public class ApprovalTemplateConfiguration : IEntityTypeConfiguration<ApprovalTemplate>
{
    public void Configure(EntityTypeBuilder<ApprovalTemplate> builder)
    {
        builder.ToTable("ApprovalTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntitySubType).HasMaxLength(100);
        builder.HasMany(x => x.Steps)
            .WithOne(x => x.Template)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApprovalTemplateStepConfiguration : IEntityTypeConfiguration<ApprovalTemplateStep>
{
    public void Configure(EntityTypeBuilder<ApprovalTemplateStep> builder)
    {
        builder.ToTable("ApprovalTemplateSteps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RoleName).HasMaxLength(100);
        builder.HasOne(x => x.Approver)
            .WithMany()
            .HasForeignKey(x => x.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.ToTable("ApprovalSteps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>();
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasOne(x => x.Approver)
            .WithMany()
            .HasForeignKey(x => x.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
