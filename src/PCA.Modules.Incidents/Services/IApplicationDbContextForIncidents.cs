using Microsoft.EntityFrameworkCore;
using PCA.Modules.Incidents.Models;

namespace PCA.Modules.Incidents.Services;

public interface IApplicationDbContextForIncidents
{
    DbSet<Incident> Incidents { get; }
    DbSet<IncidentUpdate> IncidentUpdates { get; }
    DbSet<IncidentDocument> IncidentDocuments { get; }
    DbSet<IncidentSequence> IncidentSequences { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
