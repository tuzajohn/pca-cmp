using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PCA.Modules.Documents.Models;

namespace PCA.Modules.Documents.Data;

public class DocumentReviewConfiguration : IEntityTypeConfiguration<DocumentReview>
{
    public void Configure(EntityTypeBuilder<DocumentReview> builder)
    {
        builder.ToTable("DocumentReviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Notes).HasMaxLength(2000);

        builder.HasIndex(x => x.DocumentId);

        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReviewedBy)
            .WithMany()
            .HasForeignKey(x => x.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
