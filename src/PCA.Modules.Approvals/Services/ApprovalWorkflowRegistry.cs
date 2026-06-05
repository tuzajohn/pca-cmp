namespace PCA.Modules.Approvals.Services;

public class ApprovalWorkflowRegistry : IApprovalWorkflowRegistry
{
    private readonly Dictionary<string, IApprovalWorkflow> _workflows = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IApprovalWorkflow workflow)
        => _workflows[workflow.EntityType] = workflow;

    public IApprovalWorkflow Resolve(string entityType)
    {
        if (_workflows.TryGetValue(entityType, out var workflow)) return workflow;
        throw new InvalidOperationException($"No approval workflow registered for entity type '{entityType}'.");
    }

    public IEnumerable<string> RegisteredEntityTypes => _workflows.Keys;
}
