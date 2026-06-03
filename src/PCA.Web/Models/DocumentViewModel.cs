using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using PCA.Modules.Documents.Models;
using PCA.Shared.Enums;

namespace PCA.Web.Models;

public class DocumentIndexViewModel
{
    public List<DocumentFolder> FolderTree { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
    public int? ActiveFolderId { get; set; }
    public string? SearchQuery { get; set; }
    public string? StatusFilter { get; set; }
    public AccessLevel? EffectiveAccess { get; set; }
}

public class DocumentCreateViewModel
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public int? FolderId { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    [Required]
    public IFormFile? File { get; set; }

    public string? ChangeNotes { get; set; }
}

public class DocumentEditViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public int? FolderId { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    public DocumentStatus Status { get; set; }
    public string OwnerId { get; set; } = string.Empty;
}

public class DocumentUploadVersionViewModel
{
    public int DocumentId { get; set; }

    [Required]
    public IFormFile? File { get; set; }

    public string? ChangeNotes { get; set; }
}

public class FolderCreateViewModel
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int? ParentId { get; set; }
}

public class FolderEditViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int? ParentId { get; set; }
}

public class PermissionGrantViewModel
{
    public int? FolderId { get; set; }
    public int? DocumentId { get; set; }

    [Required]
    public PermissionSubjectType SubjectType { get; set; }

    [Required]
    public string SubjectId { get; set; } = string.Empty;

    [Required]
    public AccessLevel AccessLevel { get; set; }
}
