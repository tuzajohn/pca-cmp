using Microsoft.EntityFrameworkCore;
using PCA.Modules.AccessManagement.Models;
using PCA.Shared.Enums;

namespace PCA.Modules.AccessManagement.Services;

public class AccessManagementService : IAccessManagementService
{
    private readonly IApplicationDbContextForAccessManagement _db;

    public AccessManagementService(IApplicationDbContextForAccessManagement db)
    {
        _db = db;
    }

    // ─── Access Requests ────────────────────────────────────────────────────

    public async Task<List<AccessRequest>> GetAllAccessRequestsAsync()
        => await _db.AccessRequests
            .Include(x => x.RequestedBy)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

    public async Task<List<AccessRequest>> GetAccessRequestsByUserAsync(string userId)
        => await _db.AccessRequests
            .Include(x => x.RequestedBy)
            .Where(x => x.RequestedById == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

    public async Task<AccessRequest?> GetAccessRequestByIdAsync(int id)
        => await _db.AccessRequests
            .Include(x => x.RequestedBy)
            .Include(x => x.ProvisionedBy)
            .Include(x => x.Comments).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<AccessRequest> CreateAccessRequestAsync(AccessRequest request)
    {
        request.CreatedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        request.Status = AccessRequestStatus.Draft;
        request.SerialNumber = string.Empty;
        _db.AccessRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<AccessRequest> UpdateAccessRequestAsync(AccessRequest request)
    {
        request.UpdatedAt = DateTime.UtcNow;
        _db.AccessRequests.Update(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<bool> SubmitAccessRequestAsync(int id, string userId)
    {
        var req = await _db.AccessRequests.FindAsync(id);
        if (req == null || req.Status != AccessRequestStatus.Draft) return false;
        req.SerialNumber = await GenerateSerialAsync("AR");
        req.Status = AccessRequestStatus.Submitted;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAccessRequestStatusAsync(int id, AccessRequestStatus status, string userId)
    {
        var req = await _db.AccessRequests.FindAsync(id);
        if (req == null) return false;
        req.Status = status;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkProvisionedAsync(int id, string userId)
    {
        var req = await _db.AccessRequests.FindAsync(id);
        if (req == null || req.Status != AccessRequestStatus.Approved) return false;
        req.Status = AccessRequestStatus.Provisioned;
        req.ProvisionedAt = DateTime.UtcNow;
        req.ProvisionedById = userId;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task AddAccessRequestCommentAsync(int id, string authorId, string content)
    {
        _db.AccessRequestComments.Add(new AccessRequestComment
        {
            AccessRequestId = id,
            AuthorId = authorId,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task<Dictionary<AccessRequestStatus, int>> GetAccessRequestStatusCountsAsync()
    {
        var counts = await _db.AccessRequests
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        return counts.ToDictionary(x => x.Status, x => x.Count);
    }

    // ─── Access Reviews ──────────────────────────────────────────────────────

    public async Task<List<AccessReview>> GetAllAccessReviewsAsync()
        => await _db.AccessReviews
            .Include(x => x.CreatedBy)
            .Include(x => x.Entries)
            .OrderByDescending(x => x.DueDate)
            .ToListAsync();

    public async Task<AccessReview?> GetAccessReviewByIdAsync(int id)
        => await _db.AccessReviews
            .Include(x => x.CreatedBy)
            .Include(x => x.Entries).ThenInclude(e => e.ReviewedBy)
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<AccessReview> CreateAccessReviewAsync(AccessReview review)
    {
        review.CreatedAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;
        review.Status = AccessReviewStatus.Scheduled;
        _db.AccessReviews.Add(review);
        await _db.SaveChangesAsync();
        return review;
    }

    public async Task UpdateAccessReviewEntryAsync(int entryId, AccessReviewEntryOutcome outcome,
        string reviewedById, string? notes)
    {
        var entry = await _db.AccessReviewEntries.FindAsync(entryId);
        if (entry == null) return;
        entry.Outcome = outcome;
        entry.ReviewedById = reviewedById;
        entry.ReviewerNotes = notes;
        entry.ReviewedAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Transition review to InProgress if still Scheduled
        var review = await _db.AccessReviews.FindAsync(entry.AccessReviewId);
        if (review?.Status == AccessReviewStatus.Scheduled)
        {
            review.Status = AccessReviewStatus.InProgress;
            review.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> CompleteAccessReviewAsync(int id, string userId)
    {
        var review = await _db.AccessReviews
            .Include(x => x.Entries)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (review == null) return false;

        review.Status = AccessReviewStatus.Completed;
        review.CompletedAt = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<AccessReview>> GetOverdueAccessReviewsAsync()
        => await _db.AccessReviews
            .Where(x => x.Status != AccessReviewStatus.Completed && x.DueDate < DateTime.UtcNow)
            .ToListAsync();

    // ─── Deprovisioning ──────────────────────────────────────────────────────

    public async Task<List<DeprovisioningEvent>> GetAllDeprovisioningEventsAsync()
        => await _db.DeprovisioningEvents
            .Include(x => x.NotifiedBy)
            .Include(x => x.SystemEntries)
            .OrderByDescending(x => x.HrNotificationReceivedAt)
            .ToListAsync();

    public async Task<List<DeprovisioningEvent>> GetDeprovisioningEventsLast12MonthsAsync()
    {
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        return await _db.DeprovisioningEvents
            .Include(x => x.NotifiedBy)
            .Include(x => x.SystemEntries)
            .Where(x => x.HrNotificationReceivedAt >= cutoff)
            .OrderByDescending(x => x.HrNotificationReceivedAt)
            .ToListAsync();
    }

    public async Task<DeprovisioningEvent?> GetDeprovisioningEventByIdAsync(int id)
        => await _db.DeprovisioningEvents
            .Include(x => x.NotifiedBy)
            .Include(x => x.CompletedBy)
            .Include(x => x.SystemEntries).ThenInclude(s => s.DeactivatedBy)
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<DeprovisioningEvent> CreateDeprovisioningEventAsync(DeprovisioningEvent evt)
    {
        evt.CreatedAt = DateTime.UtcNow;
        evt.UpdatedAt = DateTime.UtcNow;
        evt.Status = DeprovisioningStatus.Notified;
        evt.SlaDeadline = evt.HrNotificationReceivedAt.AddHours(24);
        evt.SerialNumber = await GenerateSerialAsync("DR");

        foreach (var entry in evt.SystemEntries)
        {
            entry.CreatedAt = DateTime.UtcNow;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        _db.DeprovisioningEvents.Add(evt);
        await _db.SaveChangesAsync();
        return evt;
    }

    public async Task<bool> UpdateSystemEntryAsync(int entryId, bool isDeactivated, string userId)
    {
        var entry = await _db.DeprovisioningSystemEntries
            .Include(x => x.DeprovisioningEvent)
            .FirstOrDefaultAsync(x => x.Id == entryId);
        if (entry == null) return false;

        entry.IsDeactivated = isDeactivated;
        entry.DeactivatedAt = isDeactivated ? DateTime.UtcNow : null;
        entry.DeactivatedById = isDeactivated ? userId : null;
        entry.UpdatedAt = DateTime.UtcNow;

        if (entry.DeprovisioningEvent?.Status == DeprovisioningStatus.Notified && isDeactivated)
        {
            entry.DeprovisioningEvent.Status = DeprovisioningStatus.InProgress;
            entry.DeprovisioningEvent.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CompleteDeprovisioningAsync(int id, string userId)
    {
        var evt = await _db.DeprovisioningEvents
            .Include(x => x.SystemEntries)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (evt == null) return false;
        if (evt.SystemEntries.Any(s => !s.IsDeactivated)) return false;

        evt.Status = DeprovisioningStatus.Completed;
        evt.CompletedAt = DateTime.UtcNow;
        evt.CompletedById = userId;
        evt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<DeprovisioningEvent>> GetOverdueDeprovisioningEventsAsync()
        => await _db.DeprovisioningEvents
            .Where(x => x.Status != DeprovisioningStatus.Completed && x.SlaDeadline < DateTime.UtcNow)
            .ToListAsync();

    public async Task<List<DeprovisioningEvent>> GetSlaWarningPendingAsync()
    {
        var warningThreshold = DateTime.UtcNow.AddHours(4);
        return await _db.DeprovisioningEvents
            .Where(x => x.Status != DeprovisioningStatus.Completed
                && x.SlaDeadline <= warningThreshold
                && x.SlaDeadline > DateTime.UtcNow
                && x.SlaWarningEmailSentAt == null)
            .ToListAsync();
    }

    // ─── Server Room Access ──────────────────────────────────────────────────

    public async Task<List<ServerRoomAccessRequest>> GetAllServerRoomRequestsAsync()
        => await _db.ServerRoomAccessRequests
            .Include(x => x.RequestedBy)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

    public async Task<ServerRoomAccessRequest?> GetServerRoomRequestByIdAsync(int id)
        => await _db.ServerRoomAccessRequests
            .Include(x => x.RequestedBy)
            .Include(x => x.Comments).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<ServerRoomAccessRequest> CreateServerRoomRequestAsync(ServerRoomAccessRequest request)
    {
        request.CreatedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        request.Status = ServerRoomAccessStatus.Draft;
        request.SerialNumber = string.Empty;
        _db.ServerRoomAccessRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<ServerRoomAccessRequest> UpdateServerRoomRequestAsync(ServerRoomAccessRequest request)
    {
        request.UpdatedAt = DateTime.UtcNow;
        _db.ServerRoomAccessRequests.Update(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<bool> SubmitServerRoomRequestAsync(int id, string userId)
    {
        var req = await _db.ServerRoomAccessRequests.FindAsync(id);
        if (req == null || req.Status != ServerRoomAccessStatus.Draft) return false;
        req.SerialNumber = await GenerateSerialAsync("SRA");
        req.Status = ServerRoomAccessStatus.Submitted;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateServerRoomStatusAsync(int id, ServerRoomAccessStatus status, string userId)
    {
        var req = await _db.ServerRoomAccessRequests.FindAsync(id);
        if (req == null) return false;
        req.Status = status;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RecordActualEntryExitAsync(int id, DateTime? entryTime, DateTime? exitTime)
    {
        var req = await _db.ServerRoomAccessRequests.FindAsync(id);
        if (req == null) return false;
        if (entryTime.HasValue) req.ActualEntryDateTime = entryTime;
        if (exitTime.HasValue) req.ActualExitDateTime = exitTime;
        req.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task AddServerRoomCommentAsync(int id, string authorId, string content)
    {
        _db.ServerRoomAccessComments.Add(new ServerRoomAccessComment
        {
            ServerRoomAccessRequestId = id,
            AuthorId = authorId,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    // ─── Serial number generation ─────────────────────────────────────────────

    private async Task<string> GenerateSerialAsync(string prefix)
    {
        var now = DateTime.UtcNow;
        var seq = await _db.AccessSequences
            .FirstOrDefaultAsync(x => x.Prefix == prefix && x.Year == now.Year && x.Month == now.Month);

        if (seq == null)
        {
            seq = new AccessSequence { Prefix = prefix, Year = now.Year, Month = now.Month, LastSequence = 1 };
            _db.AccessSequences.Add(seq);
        }
        else
        {
            seq.LastSequence++;
            _db.AccessSequences.Update(seq);
        }

        await _db.SaveChangesAsync();
        return $"PCA-{prefix}-{now.Year}{now.Month:D2}-{seq.LastSequence:D4}";
    }
}
