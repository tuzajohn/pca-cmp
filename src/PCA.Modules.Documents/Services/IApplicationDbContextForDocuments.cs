using Microsoft.EntityFrameworkCore;
using PCA.Modules.Documents.Models;

namespace PCA.Modules.Documents.Services;

public interface IApplicationDbContextForDocuments
{
    DbSet<DocumentFolder> DocumentFolders { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentVersion> DocumentVersions { get; }
    DbSet<FolderPermission> FolderPermissions { get; }
    DbSet<DocumentPermission> DocumentPermissions { get; }
    DbSet<DocumentSequence> DocumentSequences { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
