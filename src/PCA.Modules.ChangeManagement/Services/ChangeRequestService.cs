using Microsoft.EntityFrameworkCore;
using PCA.Modules.ChangeManagement.Models;
using PCA.Shared.Enums;

namespace PCA.Modules.ChangeManagement.Services;

public interface IApplicationDbContextForCM
{
    DbSet<ChangeRequest> ChangeRequests { get; }
    DbSet<ChangeRequestComment> ChangeRequestComments { get; }
    DbSet<ChangeRequestSequence> ChangeRequestSequences { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public class ChangeRequestService : IChangeRequestService
{
    private readonly IApplicationDbContextForCM _db;

    public ChangeRequestService(IApplicationDbContextForCM db)
    {
        _db = db;
    }

    public async Task<List<ChangeRequest>> GetAllAsync()
    {
        return await _db.ChangeRequests
            .Include(x => x.RequestedBy)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<ChangeRequest>> GetByUserAsync(string userId)
    {
        return await _db.ChangeRequests
            .Include(x => x.RequestedBy)
            .Where(x => x.RequestedById == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<ChangeRequest?> GetByIdAsync(int id)
    {
        return await _db.ChangeRequests
            .Include(x => x.RequestedBy)
            .Include(x => x.Comments)
                .ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<ChangeRequest> CreateAsync(ChangeRequest changeRequest)
    {
        changeRequest.CreatedAt = DateTime.UtcNow;
        changeRequest.UpdatedAt = DateTime.UtcNow;
        changeRequest.Status = ChangeStatus.Draft;
        _db.ChangeRequests.Add(changeRequest);
        await _db.SaveChangesAsync();
        return changeRequest;
    }

    public async Task<ChangeRequest> UpdateAsync(ChangeRequest changeRequest)
    {
        changeRequest.UpdatedAt = DateTime.UtcNow;
        _db.ChangeRequests.Update(changeRequest);
        await _db.SaveChangesAsync();
        return changeRequest;
    }

    public async Task<bool> SubmitAsync(int id, string userId)
    {
        var cr = await _db.ChangeRequests.FindAsync(id);
        if (cr == null || cr.RequestedById != userId) return false;
        if (cr.Status != ChangeStatus.Draft) return false;

        cr.SerialNumber = await GenerateSerialNumberAsync();
        cr.Status = ChangeStatus.Submitted;
        cr.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(int id, ChangeStatus status, string userId)
    {
        var cr = await _db.ChangeRequests.FindAsync(id);
        if (cr == null) return false;
        cr.Status = status;
        cr.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ChangeRequest>> GetRecentAsync(int count = 10)
    {
        return await _db.ChangeRequests
            .Include(x => x.RequestedBy)
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<Dictionary<ChangeStatus, int>> GetStatusCountsAsync()
    {
        var counts = await _db.ChangeRequests
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        return counts.ToDictionary(x => x.Status, x => x.Count);
    }

    public async Task AddCommentAsync(int changeRequestId, string authorId, string content)
    {
        var comment = new ChangeRequestComment
        {
            ChangeRequestId = changeRequestId,
            AuthorId = authorId,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ChangeRequestComments.Add(comment);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> SubmitPirAsync(int id, string userId, ImplementationOutcome outcome,
        DateTime? actualDate, string? issues, string? lessons, bool rollbackExecuted, string? notes)
    {
        var cr = await _db.ChangeRequests.FindAsync(id);
        if (cr == null) return false;
        if (cr.Status != ChangeStatus.Implemented) return false;

        cr.PirOutcome = outcome;
        cr.PirActualDate = actualDate;
        cr.PirIssuesEncountered = issues;
        cr.PirLessonsLearned = lessons;
        cr.PirRollbackExecuted = rollbackExecuted;
        cr.PirClosureNotes = notes;
        cr.PirCompletedById = userId;
        cr.Status = ChangeStatus.Closed;
        cr.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string> GenerateSerialNumberAsync()
    {
        var now = DateTime.UtcNow;
        var year = now.Year;
        var month = now.Month;

        var seq = await _db.ChangeRequestSequences
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month);

        if (seq == null)
        {
            seq = new ChangeRequestSequence { Year = year, Month = month, LastSequence = 1 };
            _db.ChangeRequestSequences.Add(seq);
        }
        else
        {
            seq.LastSequence++;
            _db.ChangeRequestSequences.Update(seq);
        }

        await _db.SaveChangesAsync();
        return $"PCA-CR-{year}{month:D2}-{seq.LastSequence:D4}";
    }
}
