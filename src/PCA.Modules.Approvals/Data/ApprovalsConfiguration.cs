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
        builder.Property(x => x.ApprovalMode).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(ApprovalMode.AllMustApprove);
        builder.Property(x => x.AutoTriggerOn).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(AutoTriggerOn.None);
        builder.Property(x => x.ConditionField).HasMaxLength(100);
        builder.Property(x => x.ConditionValue).HasMaxLength(200);
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

public class ApprovalFlowConfiguration : IEntityTypeConfiguration<ApprovalFlow>
{
    public void Configure(EntityTypeBuilder<ApprovalFlow> builder)
    {
        builder.ToTable("ApprovalFlows");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ReturnComment).HasMaxLength(2000);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InitiatedBy)
            .WithMany()
            .HasForeignKey(x => x.InitiatedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReturnedBy)
            .WithMany()
            .HasForeignKey(x => x.ReturnedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Steps)
            .WithOne(x => x.Flow)
            .HasForeignKey(x => x.FlowId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
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
