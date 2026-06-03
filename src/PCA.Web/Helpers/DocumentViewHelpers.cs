using PCA.Shared.Enums;

namespace PCA.Web.Helpers;

public static class DocumentViewHelpers
{
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / 1048576.0:F1} MB";
    }

    public static string StatusClass(DocumentStatus s)
    {
        return s switch
        {
            DocumentStatus.Active => "status-approved",
            DocumentStatus.Draft => "status-draft",
            DocumentStatus.UnderReview => "status-submitted",
            DocumentStatus.Superseded => "status-implemented",
            DocumentStatus.Retired => "status-closed",
            _ => string.Empty
        };
    }

    public static string FileIcon(string ext)
    {
        return ext.ToUpperInvariant() switch
        {
            "PDF" => "bi-file-earmark-pdf",
            "DOC" => "bi-file-earmark-word",
            "DOCX" => "bi-file-earmark-word",
            "XLS" => "bi-file-earmark-excel",
            "XLSX" => "bi-file-earmark-excel",
            "PPT" => "bi-file-earmark-ppt",
            "PPTX" => "bi-file-earmark-ppt",
            "PNG" => "bi-file-earmark-image",
            "JPG" => "bi-file-earmark-image",
            "JPEG" => "bi-file-earmark-image",
            "GIF" => "bi-file-earmark-image",
            "ZIP" => "bi-file-earmark-zip",
            "TXT" => "bi-file-earmark-text",
            "CSV" => "bi-file-earmark-spreadsheet",
            _ => "bi-file-earmark"
        };
    }
}
