using System.ComponentModel.DataAnnotations;
using PCA.Shared.Enums;

namespace PCA.Web.Models;

// ─── Access Requests ─────────────────────────────────────────────────────────

public class AccessRequestCreateViewModel
{
    [Required, MaxLength(300), Display(Name = "Employee Name")]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(100), Display(Name = "Employee / Payroll ID")]
    public string EmployeeId { get; set; } = string.Empty;

    [MaxLength(200), Display(Name = "Department")]
    public string Department { get; set; } = string.Empty;

    [MaxLength(200), Display(Name = "Job Title")]
    public string JobTitle { get; set; } = string.Empty;

    [Required, MaxLength(300), Display(Name = "System / Application")]
    public string SystemName { get; set; } = string.Empty;

    [Required, Display(Name = "Access Type")]
    public AccessType AccessType { get; set; }

    [Required, Display(Name = "Access Details / Roles Requested")]
    public string AccessDetails { get; set; } = string.Empty;

    [Required, Display(Name = "Business Justification")]
    public string Justification { get; set; } = string.Empty;

    [Required, Display(Name = "Access Required By")]
    public DateTime RequestedByDate { get; set; } = DateTime.Today.AddDays(7);

    [Display(Name = "Access Expiry Date (leave blank for permanent)")]
    public DateTime? AccessExpiryDate { get; set; }

    [Display(Name = "Privileged / Sensitive Access?")]
    public bool IsPrivileged { get; set; }
}

public class AccessRequestEditViewModel : AccessRequestCreateViewModel
{
    public int Id { get; set; }
}

// ─── Access Reviews ───────────────────────────────────────────────────────────

public class AccessReviewCreateViewModel
{
    [Required, MaxLength(300), Display(Name = "Review Title")]
    public string Title { get; set; } = string.Empty;

    [Required, Display(Name = "Review Cycle")]
    public AccessReviewCycle Cycle { get; set; }

    [Required, Display(Name = "Year")]
    public int Year { get; set; } = DateTime.Today.Year;

    [Required, Display(Name = "Quarter / Period")]
    public int Quarter { get; set; } = ((DateTime.Today.Month - 1) / 3) + 1;

    [Required, Display(Name = "Review Period Start")]
    public DateTime ReviewPeriodStart { get; set; } = DateTime.Today;

    [Required, Display(Name = "Review Period End")]
    public DateTime ReviewPeriodEnd { get; set; } = DateTime.Today.AddMonths(3);

    [Required, Display(Name = "Due Date")]
    public DateTime DueDate { get; set; } = DateTime.Today.AddMonths(1);

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public List<AccessReviewEntryInputModel> Entries { get; set; } = new();
}

public class AccessReviewEntryInputModel
{
    [Required, MaxLength(300), Display(Name = "Employee Name")]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(200), Display(Name = "Department")]
    public string Department { get; set; } = string.Empty;

    [Required, MaxLength(300), Display(Name = "System / Application")]
    public string SystemName { get; set; } = string.Empty;

    [MaxLength(300), Display(Name = "Current Access Level")]
    public string CurrentAccessLevel { get; set; } = string.Empty;
}

// ─── Deprovisioning ───────────────────────────────────────────────────────────

public class DeprovisioningCreateViewModel
{
    [Required, MaxLength(300), Display(Name = "Employee Name")]
    public string EmployeeName { get; set; } = string.Empty;

    [MaxLength(100), Display(Name = "Employee / Payroll ID")]
    public string EmployeeId { get; set; } = string.Empty;

    [MaxLength(200), Display(Name = "Department")]
    public string Department { get; set; } = string.Empty;

    [MaxLength(200), Display(Name = "Job Title")]
    public string JobTitle { get; set; } = string.Empty;

    [Required, Display(Name = "Trigger")]
    public DeprovisioningTrigger Trigger { get; set; }

    [Display(Name = "Trigger Details / Reason")]
    public string TriggerDetails { get; set; } = string.Empty;

    [Required, Display(Name = "HR Notification Received At")]
    public DateTime HrNotificationReceivedAt { get; set; } = DateTime.Now;

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public List<SystemEntryInputModel> SystemEntries { get; set; } = new();
}

public class SystemEntryInputModel
{
    [Required, MaxLength(300), Display(Name = "System Name")]
    public string SystemName { get; set; } = string.Empty;

    [Display(Name = "Access to Revoke")]
    public string AccessDescription { get; set; } = string.Empty;
}

// ─── Server Room Access ───────────────────────────────────────────────────────

public class ServerRoomCreateViewModel
{
    [Required, MaxLength(300), Display(Name = "Visitor Name")]
    public string VisitorName { get; set; } = string.Empty;

    [MaxLength(200), Display(Name = "Visitor Title / Role")]
    public string VisitorTitle { get; set; } = string.Empty;

    [MaxLength(300), Display(Name = "Company / Department")]
    public string VisitorCompany { get; set; } = string.Empty;

    [Display(Name = "External Visitor?")]
    public bool IsExternal { get; set; }

    [Required, Display(Name = "Purpose of Visit")]
    public string Purpose { get; set; } = string.Empty;

    [Required, Display(Name = "Planned Entry Date & Time")]
    public DateTime PlannedEntryDateTime { get; set; } = DateTime.Now.AddHours(1);

    [Required, Display(Name = "Planned Exit Date & Time")]
    public DateTime PlannedExitDateTime { get; set; } = DateTime.Now.AddHours(3);

    [MaxLength(500), Display(Name = "Written Request Reference")]
    public string? WrittenRequestReference { get; set; }

    [MaxLength(300), Display(Name = "Escorted By")]
    public string? EscortedBy { get; set; }
}

public class ServerRoomEditViewModel : ServerRoomCreateViewModel
{
    public int Id { get; set; }
}

public class RecordEntryExitViewModel
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string VisitorName { get; set; } = string.Empty;
    public DateTime? ActualEntryDateTime { get; set; }
    public DateTime? ActualExitDateTime { get; set; }
}
