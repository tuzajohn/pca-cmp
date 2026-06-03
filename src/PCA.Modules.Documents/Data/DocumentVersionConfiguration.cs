using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PCA.Modules.Documents.Models;

namespace PCA.Modules.Documents.Data;

public class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("DocumentVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ChangeNotes).HasColumnType("text");

        builder.HasOne(x => x.Document)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UploadedBy)
            .WithMany()
            .HasForeignKey(x => x.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
