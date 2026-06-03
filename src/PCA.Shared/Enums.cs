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
    Rejected
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
