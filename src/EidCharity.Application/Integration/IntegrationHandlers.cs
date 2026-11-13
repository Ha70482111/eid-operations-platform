using System.Text.Json;
using EidCharity.Application.Collections;
using EidCharity.Application.Projects;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Integration;
using EidCharity.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Application.Integration;

/// <summary>Consumers for messages exchanged between the two systems. Each is idempotent (inbox by key).</summary>
public class IntegrationHandlers(IEidDb db)
{
    public async Task HandleAsync(OutboxMessage m, CancellationToken ct)
    {
        if (await db.Inbox.AnyAsync(i => i.IdempotencyKey == m.IdempotencyKey, ct)) { await AckMappingAsync(m, ct); return; }
        switch (m.Type)
        {
            case MessageTypes.MappingPublished: await OnMappingAsync(JsonSerializer.Deserialize<MappingPublishedPayload>(m.Payload)!, ct); break;
            case MessageTypes.CollectionResult: await OnResultAsync(JsonSerializer.Deserialize<CollectionResultPayload>(m.Payload)!, ct); break;
            default: throw new InvalidOperationException($"Unknown message type {m.Type}");
        }
        db.Inbox.Add(new InboxMessage { IdempotencyKey = m.IdempotencyKey, Type = m.Type, ProcessedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await AckMappingAsync(m, ct);
    }

    /// <summary>Collections side: stores the reference data it may not edit.</summary>
    private async Task OnMappingAsync(MappingPublishedPayload p, CancellationToken ct)
    {
        var e = await db.ProjectCatalog.FirstOrDefaultAsync(x => x.ProjectId == p.ProjectId, ct);
        if (e is null) { e = new ProjectCatalogEntry { ProjectId = p.ProjectId }; db.ProjectCatalog.Add(e); }
        else if (e.UpdatedAt > p.PublishedAt) return; // out-of-order older message: ignore
        e.ProgramId = p.ProgramId; e.CenterId = p.CenterId; e.ProjectCode = p.ProjectCode; e.ProgramCode = p.ProgramCode; e.NameAr = p.NameAr; e.NameEn = p.NameEn;
        e.ProgramNameAr = p.ProgramNameAr; e.ProgramNameEn = p.ProgramNameEn; e.CenterNameAr = p.CenterNameAr; e.CenterNameEn = p.CenterNameEn;
        e.FinancialItemNumber = p.FinancialItemNumber; e.FinancialItemNameAr = p.FinancialItemNameAr; e.Visual = p.Visual; e.QuickAmounts = p.QuickAmounts;
        e.CollectionCap = p.CollectionCap; e.IsAvailable = p.IsAvailable; e.UnavailableReason = p.UnavailableReason; e.MappingVersion = p.MappingVersion; e.UpdatedAt = p.PublishedAt;
    }

    /// <summary>Projects side: pledges raise expected only; confirmation raises collected; refunds lower net.</summary>
    private async Task OnResultAsync(CollectionResultPayload p, CancellationToken ct)
    {
        if (await db.CollectionResults.AnyAsync(c => c.IdempotencyKey == p.Key, ct)) return;
        db.CollectionResults.Add(new CollectionResult
        {
            IdempotencyKey = p.Key, ReceivedAt = DateTime.UtcNow, OccurredAt = p.OccurredAt, ProjectId = p.ProjectId, FinancialItemId = p.FinancialItemId, LicenseId = p.LicenseId,
            Kind = p.Kind, Amount = p.Amount, Currency = p.Currency, TransactionNumber = p.TransactionNumber, ReceiptNumber = p.ReceiptNumber, AgreementNumber = p.AgreementNumber,
            MappingVersion = p.MappingVersion,
        });
    }

    private async Task AckMappingAsync(OutboxMessage m, CancellationToken ct)
    {
        if (m.Type != MessageTypes.MappingPublished) return;
        var p = JsonSerializer.Deserialize<MappingPublishedPayload>(m.Payload)!;
        await db.Projects.Where(x => x.Id == p.ProjectId && x.MappingVersion == p.MappingVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.MappingSyncStatus, SyncStatus.Succeeded).SetProperty(x => x.MappingSyncedAt, DateTime.UtcNow).SetProperty(x => x.MappingSyncError, (string?)null), ct);
    }

    public async Task MarkFailedAsync(OutboxMessage m, string error, CancellationToken ct)
    {
        if (m.Type != MessageTypes.MappingPublished) return;
        var p = JsonSerializer.Deserialize<MappingPublishedPayload>(m.Payload)!;
        await db.Projects.Where(x => x.Id == p.ProjectId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.MappingSyncStatus, SyncStatus.Failed).SetProperty(x => x.MappingSyncError, error), ct);
    }
}
