using Microsoft.EntityFrameworkCore;
using PCA.Modules.AccessManagement.Models;
using PCA.Shared;
using PCA.Shared.Enums;

namespace PCA.Modules.AccessManagement.Services;

public interface IApplicationDbContextForAccessManagement
{
    DbSet<AccessRequest> AccessRequests { get; }
    DbSet<AccessRequestComment> AccessRequestComments { get; }
    DbSet<AccessReview> AccessReviews { get; }
    DbSet<AccessReviewEntry> AccessReviewEntries { get; }
    DbSet<DeprovisioningEvent> DeprovisioningEvents { get; }
    DbSet<DeprovisioningSystemEntry> DeprovisioningSystemEntries { get; }
    DbSet<ServerRoomAccessRequest> ServerRoomAccessRequests { get; }
    DbSet<ServerRoomAccessComment> ServerRoomAccessComments { get; }
    DbSet<AccessSequence> AccessSequences { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IAccessManagementService
{
    // Access Requests
    Task<List<AccessRequest>> GetAllAccessRequestsAsync();
    Task<List<AccessRequest>> GetAccessRequestsByUserAsync(string userId);
    Task<PagedResult<AccessRequest>> GetAccessRequestsPagedAsync(string? userId, string? status, string? system, DateTime? from, DateTime? to, int page, int pageSize, string? sortCol = null, string? sortDir = null);
    Task<AccessRequest?> GetAccessRequestByIdAsync(int id);
    Task<AccessRequest> CreateAccessRequestAsync(AccessRequest request);
    Task<AccessRequest> UpdateAccessRequestAsync(AccessRequest request);
    Task<bool> SubmitAccessRequestAsync(int id, string userId);
    Task<bool> UpdateAccessRequestStatusAsync(int id, AccessRequestStatus status, string userId);
    Task<bool> MarkProvisionedAsync(int id, string userId);
    Task AddAccessRequestCommentAsync(int id, string authorId, string content);
    Task<Dictionary<AccessRequestStatus, int>> GetAccessRequestStatusCountsAsync();

    // Access Reviews
    Task<List<AccessReview>> GetAllAccessReviewsAsync();
    Task<AccessReview?> GetAccessReviewByIdAsync(int id);
    Task<AccessReview> CreateAccessReviewAsync(AccessReview review);
    Task UpdateAccessReviewEntryAsync(int entryId, AccessReviewEntryOutcome outcome, string reviewedById, string? notes);
    Task<bool> CompleteAccessReviewAsync(int id, string userId);
    Task<List<AccessReview>> GetOverdueAccessReviewsAsync();

    // Deprovisioning
    Task<List<DeprovisioningEvent>> GetAllDeprovisioningEventsAsync();
    Task<List<DeprovisioningEvent>> GetDeprovisioningEventsLast12MonthsAsync();
    Task<PagedResult<DeprovisioningEvent>> GetDeprovisioningPagedAsync(string? status, bool allTime, int page, int pageSize, string? sortCol = null, string? sortDir = null);
    Task<DeprovisioningEvent?> GetDeprovisioningEventByIdAsync(int id);
    Task<DeprovisioningEvent> CreateDeprovisioningEventAsync(DeprovisioningEvent evt);
    Task<bool> UpdateSystemEntryAsync(int entryId, bool isDeactivated, string userId);
    Task<bool> CompleteDeprovisioningAsync(int id, string userId);
    Task<List<DeprovisioningEvent>> GetOverdueDeprovisioningEventsAsync();
    Task<List<DeprovisioningEvent>> GetSlaWarningPendingAsync();

    // Server Room Access
    Task<List<ServerRoomAccessRequest>> GetAllServerRoomRequestsAsync();
    Task<PagedResult<ServerRoomAccessRequest>> GetServerRoomRequestsPagedAsync(string? status, int page, int pageSize, string? sortCol = null, string? sortDir = null);
    Task<ServerRoomAccessRequest?> GetServerRoomRequestByIdAsync(int id);
    Task<ServerRoomAccessRequest> CreateServerRoomRequestAsync(ServerRoomAccessRequest request);
    Task<ServerRoomAccessRequest> UpdateServerRoomRequestAsync(ServerRoomAccessRequest request);
    Task<bool> SubmitServerRoomRequestAsync(int id, string userId);
    Task<bool> UpdateServerRoomStatusAsync(int id, ServerRoomAccessStatus status, string userId);
    Task<bool> RecordActualEntryExitAsync(int id, DateTime? entryTime, DateTime? exitTime);
    Task AddServerRoomCommentAsync(int id, string authorId, string content);
}
