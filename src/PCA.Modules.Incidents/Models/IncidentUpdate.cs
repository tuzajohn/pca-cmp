using PCA.Modules.Identity.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.Incidents.Models;

public class IncidentUpdate : BaseEntity
{
    public int IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }

    public string Content { get; set; } = string.Empty;
    public IncidentUpdateType UpdateType { get; set; } = IncidentUpdateType.Comment;

    public IncidentStatus? OldStatus { get; set; }
    public IncidentStatus? NewStatus { get; set; }
}
