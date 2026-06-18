using System.ComponentModel.DataAnnotations;
using PCA.Modules.Invoicing.Models;

namespace PCA.Web.Models;

public class InvoiceLenderCreateViewModel
{
    [Required, Display(Name = "Lender Name")]
    public string Name { get; set; } = string.Empty;

    [Required, Display(Name = "Company Type")]
    public string CompanyType { get; set; } = string.Empty;

    [Required, Display(Name = "Deduction Code")]
    public string DeductionCode { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public class InvoiceLenderBulkCreateViewModel
{
    public string CompanyType { get; set; } = string.Empty;
    public List<InvoiceLenderBulkItem> Lenders { get; set; } = new();
}

public class InvoiceLenderBulkItem
{
    public bool Selected { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeductionCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class InvoiceLenderEditViewModel : InvoiceLenderCreateViewModel
{
    public int Id { get; set; }
}

public class InvoiceRecipientCreateViewModel
{
    [Required, Display(Name = "Full Name")]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Default recipient for new schedules")]
    public bool IsDefault { get; set; }
}

public class InvoiceRecipientEditViewModel : InvoiceRecipientCreateViewModel
{
    public int Id { get; set; }
}

public class InvoiceScheduleCreateViewModel
{
    [Required, Display(Name = "Schedule Name")]
    public string Name { get; set; } = string.Empty;

    [Required, Display(Name = "Lender")]
    public int LenderId { get; set; }

    [Required, Display(Name = "Frequency")]
    public InvoiceFrequency Frequency { get; set; } = InvoiceFrequency.Monthly;

    [Display(Name = "Day of Week")]
    public int? DayOfWeek { get; set; }

    [Display(Name = "Day of Month")]
    [Range(1, 31)]
    public int? DayOfMonth { get; set; }

    [Required, Display(Name = "Time of Day")]
    public TimeOnly TimeOfDay { get; set; } = new TimeOnly(6, 0);

    [Display(Name = "Enabled")]
    public bool IsEnabled { get; set; } = true;

    [Display(Name = "Split IPPS and HCM into separate sheets")]
    public bool SplitSheets { get; set; }

    public List<int> SelectedRecipientIds { get; set; } = new();
}

public class InvoiceScheduleEditViewModel : InvoiceScheduleCreateViewModel
{
    public int Id { get; set; }
}
