namespace PCA.Shared.Enums;

public enum ChangeStatus
{
    Draft,
    Submitted,
    UnderReview,
    Approved,
    Rejected,
    Implemented,
    Closed
}

public enum ChangeType
{
    Standard,
    Emergency,
    Normal
}

public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    ReturnedForEdit
}

public enum ApprovalOutcome
{
    StillPending,
    AllApproved,
    AnyRejected,
    ReturnedForEdit
}

public enum StagingTestedStatus
{
    Yes,
    No,
    NA
}

public enum ImplementationOutcome
{
    Successful,
    PartiallySuccessful,
    Failed,
    RolledBack
}

public enum DocumentStatus
{
    Draft,
    UnderReview,
    Active,
    Superseded,
    Retired
}

public enum AccessLevel
{
    Viewer,
    Contributor,
    Manager
}

public enum PermissionSubjectType
{
    User,
    Role
}

public enum IncidentStatus
{
    Open,
    InProgress,
    OnHold,
    Resolved,
    Closed
}

public enum IncidentCategory
{
    Infrastructure,
    Application,
    Security,
    Network,
    Process,
    Other
}

public enum IncidentSeverity
{
    S1Critical,
    S2High,
    S3Medium,
    S4Low
}

public enum IncidentUpdateType
{
    Comment,
    StatusChange,
    Assignment,
    Resolution
}

// Access Management

public enum AccessRequestStatus
{
    Draft,
    Submitted,
    UnderReview,
    Approved,
    Rejected,
    Provisioned,
    Cancelled
}

public enum AccessType
{
    ReadOnly,
    ReadWrite,
    Administrative,
    Privileged,
    ServiceAccount,
    TemporaryAccess
}

public enum AccessReviewStatus
{
    Scheduled,
    InProgress,
    Completed,
    Overdue
}

public enum AccessReviewCycle
{
    Quarterly,
    SemiAnnual
}

public enum AccessReviewEntryOutcome
{
    Pending,
    Retain,
    Revoke,
    Modify
}

public enum DeprovisioningStatus
{
    Notified,
    InProgress,
    Completed,
    Overdue
}

public enum DeprovisioningTrigger
{
    Termination,
    RoleChange,
    LeaveOfAbsence,
    Other
}

public enum ServerRoomAccessStatus
{
    Draft,
    Submitted,
    UnderReview,
    Approved,
    Rejected,
    Cancelled
}

