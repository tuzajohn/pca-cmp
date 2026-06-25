namespace PCA.Modules.Identity.Models;

public static class AppModules
{
    public const string ClaimType = "Module";

    public const string ChangeManagement = "ChangeManagement";
    public const string Incidents        = "Incidents";
    public const string AccessManagement = "AccessManagement";
    public const string Documents        = "Documents";
    public const string Invoicing        = "Invoicing";
    public const string CRB             = "CRB";

    public static readonly IReadOnlyList<(string Key, string Label, string Description)> All =
    [
        (ChangeManagement, "Change Management",  "Change requests, approvals workflow"),
        (Incidents,        "Incidents",           "Incident logging and tracking"),
        (AccessManagement, "Access Management",   "Access requests, reviews, deprovisioning, server room"),
        (Documents,        "Documents",           "Document repository and version control"),
        (Invoicing,        "Invoicing",           "Invoice schedules, lenders, recipients"),
        (CRB,             "CRB",                 "CRB payroll deductions report generator"),
    ];
}
