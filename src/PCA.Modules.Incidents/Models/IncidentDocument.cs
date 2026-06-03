using PCA.Modules.Identity.Models;
using PCA.Shared;

namespace PCA.Modules.Incidents.Models;

public class IncidentDocument : BaseEntity
{
    public int IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public int DocumentId { get; set; }

    public string LinkedById { get; set; } = string.Empty;
    public ApplicationUser? LinkedBy { get; set; }
}
