using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PCA.Modules.Documents.Models;

namespace PCA.Modules.Documents.Data;

public class FolderPermissionConfiguration : IEntityTypeConfiguration<FolderPermission>
{
    public void Configure(EntityTypeBuilder<FolderPermission> builder)
    {
        builder.ToTable("FolderPermissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubjectType).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.SubjectId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.AccessLevel).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Folder)
            .WithMany(x => x.Permissions)
            .HasForeignKey(x => x.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
