namespace PCA.Modules.Approvals.Services;

public interface IApprovalWorkflowRegistry
{
    void Register(IApprovalWorkflow workflow);
    IApprovalWorkflow Resolve(string entityType);
    IEnumerable<string> RegisteredEntityTypes { get; }
}
