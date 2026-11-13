using EidCharity.Application.Common;
using EidCharity.Application.Integration;
using EidCharity.Application.Projects;
using EidCharity.Domain;
using EidCharity.Domain.Collections;
using EidCharity.Domain.Integration;
using EidCharity.Domain.Projects;
using EidCharity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EidCharity.Infrastructure.Jobs;

/// <summary>Delivers outbox messages between the two systems with retries and exponential backoff.</summary>
public class OutboxDispatcher(IServiceScopeFactory scopes, ILogger<OutboxDispatcher> log) : BackgroundService
{
    public const int MaxAttempts = 8;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stop);
        while (!stop.IsCancellationRequested)
        {
            try { await DispatchBatchAsync(scopes, log, stop); }
            catch (Exception ex) when (!stop.IsCancellationRequested) { log.LogError(ex, "Outbox dispatch loop failed"); }
            await Task.Delay(TimeSpan.FromSeconds(2), stop);
        }
    }

    public static async Task<int> DispatchBatchAsync(IServiceScopeFactory scopes, ILogger log, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EidDbContext>();
        var now = DateTime.UtcNow;
        var batch = await db.Outbox.Where(o => o.State == OutboxState.Pending && (o.NextAttemptAt == null || o.NextAttemptAt <= now))
            .OrderBy(o => o.Id).Take(50).ToListAsync(ct);
        foreach (var m in batch)
        {
            using var inner = scopes.CreateScope();
            var handlers = inner.ServiceProvider.GetRequiredService<IntegrationHandlers>();
            try
            {
                await handlers.HandleAsync(m, ct);
                m.State = OutboxState.Succeeded; m.ProcessedAt = DateTime.UtcNow; m.LastError = null;
            }
            catch (Exception ex)
            {
                m.Attempts++;
                m.LastError = ex.GetBaseException().Message;
                m.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, m.Attempts) * 5);
                if (m.Attempts >= MaxAttempts) m.State = OutboxState.Failed;
                log.LogWarning(ex, "Outbox message {Id} ({Type}) failed attempt {Attempt}", m.Id, m.Type, m.Attempts);
                // Never shown as a successful mapping until acknowledged.
                using var s3 = scopes.CreateScope();
                await s3.ServiceProvider.GetRequiredService<IntegrationHandlers>().MarkFailedAsync(m, m.LastError, ct);
            }
            await db.SaveChangesAsync(ct);
        }
        return batch.Count;
    }
}

/// <summary>License expiry and cap thresholds, overdue installments, expired online holds, daily status refresh.</summary>
public class AlertsJob(IServiceScopeFactory scopes, ILogger<AlertsJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stop);
        while (!stop.IsCancellationRequested)
        {
            try { await RunOnceAsync(stop); }
            catch (Exception ex) when (!stop.IsCancellationRequested) { log.LogError(ex, "Alerts job failed"); }
            await Task.Delay(TimeSpan.FromMinutes(15), stop);
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<EidDbContext>();
        var settings = sp.GetRequiredService<SettingsService>();
        var notify = sp.GetRequiredService<NotificationService>();
        var licenses = ActivatorUtilities.CreateInstance<LicenseService>(sp, SystemCurrentUser.Instance);
        var today = LicenseService.Today;

        // 1) License expiry & cap thresholds
        var days = await settings.GetIntAsync(SettingKeys.LicenseExpiryAlertDays, 30, ct);
        var percents = (await settings.GetAsync(SettingKeys.CapAlertPercents, "80,95", ct)).Split(',').Select(s => int.TryParse(s, out var v) ? v : 0).Where(v => v > 0).ToList();
        foreach (var row in await licenses.ListAsync(ct: ct))
        {
            if (row.Status == LicenseStatus.Active && row.DaysToExpiry <= days)
                await notify.NotifyAsync($"الترخيص {row.License.Number} ينتهي خلال {row.DaysToExpiry} يومًا", $"License {row.License.Number} expires in {row.DaysToExpiry} days",
                    "/projects/licenses", Perm.LicensesView, severity: row.DaysToExpiry <= 7 ? "danger" : "warning", dedupKey: $"lic-exp:{row.License.Id}:{(row.DaysToExpiry <= 7 ? 7 : days)}", ct: ct);
            foreach (var pct in percents.Where(p => row.Usage.UsedPercent >= p))
                await notify.NotifyAsync($"الترخيص {row.License.Number} بلغ {pct}% من السقف", $"License {row.License.Number} reached {pct}% of its cap",
                    "/projects/licenses", Perm.LicensesView, severity: pct >= 95 ? "danger" : "warning", dedupKey: $"lic-cap:{row.License.Id}:{pct}", ct: ct);
        }

        // 2) Overdue installments (late payment never becomes collected automatically)
        var reminder = await settings.GetIntAsync(SettingKeys.InstallmentReminderDays, 7, ct);
        var installments = await db.Installments.Where(i => i.Status != InstallmentStatus.Paid && i.Status != InstallmentStatus.Cancelled).ToListAsync(ct);
        foreach (var i in installments)
        {
            var s = i.Compute(today);
            if (s != i.Status) i.Status = s;
            if (s == InstallmentStatus.Overdue)
                await notify.NotifyAsync($"دفعة متأخرة رقم {i.Seq} في اتفاقية #{i.AgreementId}", $"Installment {i.Seq} overdue (agreement #{i.AgreementId})",
                    $"/collections/agreements/{i.AgreementId}", Perm.AgreementsManage, severity: "danger", dedupKey: $"inst-late:{i.Id}", ct: ct);
            else if (i.DueDate.DayNumber - today.DayNumber <= reminder)
                await notify.NotifyAsync($"دفعة مستحقة قريبًا رقم {i.Seq} في اتفاقية #{i.AgreementId}", $"Installment {i.Seq} due soon (agreement #{i.AgreementId})",
                    $"/collections/agreements/{i.AgreementId}", Perm.AgreementsManage, dedupKey: $"inst-due:{i.Id}", ct: ct);
        }
        await db.SaveChangesAsync(ct);

        // 3) Online holds released after the approved timeout
        var holdMinutes = await settings.GetIntAsync(SettingKeys.PendingHoldMinutes, 60, ct);
        var cutoff = DateTime.UtcNow.AddMinutes(-holdMinutes);
        var expired = await db.Donations.Include(d => d.Lines).Where(d => d.Status == DonationStatus.PendingVerification && d.PaymentMethod == PaymentMethod.Online && d.OccurredAt < cutoff).ToListAsync(ct);
        var outbox = sp.GetRequiredService<OutboxPublisher>();
        foreach (var d in expired)
        {
            d.Status = DonationStatus.Failed; d.FailureReason = "انتهت مهلة الحجز المعتمدة";
            foreach (var l in d.Lines)
            {
                var key = $"donation:{d.Id}:line:{l.Id}:{Domain.Projects.CollectionResultKind.PendingCancelled}";
                await outbox.PublishAsync(MessageTypes.CollectionResult, new Application.Collections.CollectionResultPayload(key, l.ProjectId, l.FinancialItemId, l.LicenseId,
                    Domain.Projects.CollectionResultKind.PendingCancelled, -l.Amount, d.Currency, DateTime.UtcNow, d.Number, null, null, l.MappingVersion), key, d.Number, ct);
            }
        }
        await db.SaveChangesAsync(ct);

        // 4) Daily availability refresh (license started/expired ⇒ project available/unavailable)
        var refresher = ActivatorUtilities.CreateInstance<Application.Collections.ProjectStatusRefresher>(sp,
            ActivatorUtilities.CreateInstance<FinanceReviewService>(sp, SystemCurrentUser.Instance));
        var approved = await db.Projects.Where(p => p.ApprovalStatus == ApprovalStatus.FinanciallyApproved && p.ExecutionStatus != ExecutionStatus.Closed
                                                   && p.CollectionStatus != CollectionStatus.Suspended).Select(p => p.Id).ToListAsync(ct);
        if (approved.Count > 0) await refresher.RefreshAsync(approved, ct);
    }
}
