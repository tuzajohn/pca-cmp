using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PCA.Modules.Invoicing.Models;

namespace PCA.Modules.Invoicing.Data;

public class InvoiceLenderConfiguration : IEntityTypeConfiguration<InvoiceLender>
{
    public void Configure(EntityTypeBuilder<InvoiceLender> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.CompanyType).HasMaxLength(20).IsRequired();
        b.Property(e => e.DeductionCode).HasMaxLength(50).IsRequired();
        b.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class InvoiceRecipientConfiguration : IEntityTypeConfiguration<InvoiceRecipient>
{
    public void Configure(EntityTypeBuilder<InvoiceRecipient> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Email).HasMaxLength(300).IsRequired();
        b.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class InvoiceScheduleConfiguration : IEntityTypeConfiguration<InvoiceSchedule>
{
    public void Configure(EntityTypeBuilder<InvoiceSchedule> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.Frequency).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.TimeOfDay).HasColumnType("time(0)");
        b.HasOne(e => e.Lender).WithMany(l => l.Schedules).HasForeignKey(e => e.LenderId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.CreatedBy).WithMany().HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class InvoiceScheduleRecipientConfiguration : IEntityTypeConfiguration<InvoiceScheduleRecipient>
{
    public void Configure(EntityTypeBuilder<InvoiceScheduleRecipient> b)
    {
        b.HasKey(e => new { e.InvoiceScheduleId, e.InvoiceRecipientId });
        b.HasOne(e => e.Schedule).WithMany(s => s.ScheduleRecipients)
            .HasForeignKey(e => e.InvoiceScheduleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.Recipient).WithMany(r => r.ScheduleRecipients)
            .HasForeignKey(e => e.InvoiceRecipientId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class InvoiceHcmRefFileConfiguration : IEntityTypeConfiguration<InvoiceHcmRefFile>
{
    public void Configure(EntityTypeBuilder<InvoiceHcmRefFile> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.MonthYear).HasMaxLength(7).IsRequired();
        b.Property(e => e.FilePath).HasMaxLength(500).IsRequired();
        b.Property(e => e.OriginalFileName).HasMaxLength(300).IsRequired();
        b.HasOne(e => e.Schedule).WithMany().HasForeignKey(e => e.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.UploadedBy).WithMany().HasForeignKey(e => e.UploadedById)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(e => new { e.ScheduleId, e.MonthYear }).IsUnique();
    }
}

public class InvoiceRunConfiguration : IEntityTypeConfiguration<InvoiceRun>
{
    public void Configure(EntityTypeBuilder<InvoiceRun> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.ErrorMessage).HasColumnType("text");
        b.Property(e => e.FilePath).HasMaxLength(500);
        b.Property(e => e.FileName).HasMaxLength(300);
        b.HasOne(e => e.Schedule).WithMany(s => s.Runs).HasForeignKey(e => e.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(e => e.TriggeredBy).WithMany().HasForeignKey(e => e.TriggeredById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
