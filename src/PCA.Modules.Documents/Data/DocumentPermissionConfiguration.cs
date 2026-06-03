using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PCA.Modules.Documents.Models;

namespace PCA.Modules.Documents.Data;

public class DocumentPermissionConfiguration : IEntityTypeConfiguration<DocumentPermission>
{
    public void Configure(EntityTypeBuilder<DocumentPermission> builder)
    {
        builder.ToTable("DocumentPermissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubjectType).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.SubjectId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.AccessLevel).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Document)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
