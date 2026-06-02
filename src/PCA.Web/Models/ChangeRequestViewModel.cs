using System.ComponentModel.DataAnnotations;
using PCA.Shared.Enums;

namespace PCA.Web.Models;

public class ChangeRequestCreateViewModel
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ChangeType Type { get; set; }

    [Required]
    public Priority Priority { get; set; }

    [Display(Name = "Target Date")]
    public DateTime? TargetDate { get; set; }
}

public class ChangeRequestEditViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ChangeType Type { get; set; }

    [Required]
    public Priority Priority { get; set; }

    [Display(Name = "Target Date")]
    public DateTime? TargetDate { get; set; }

    [MaxLength(4000), Display(Name = "Implementation Notes")]
    public string? ImplementationNotes { get; set; }
}

