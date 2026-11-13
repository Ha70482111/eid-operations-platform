using EidCharity.Application.Admin;
using EidCharity.Application.Collections;
using EidCharity.Application.Common;
using EidCharity.Application.Integration;
using EidCharity.Application.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace EidCharity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddEidApplication(this IServiceCollection s)
    {
        s.AddScoped<AuditService>();
        s.AddScoped<AttachmentService>();
        s.AddScoped<SettingsService>();
        s.AddScoped<NotificationService>();
        s.AddScoped<OutboxPublisher>();
        s.AddScoped<ApprovalMatrixService>();

        s.AddScoped<ProjectService>();
        s.AddScoped<LicenseService>();
        s.AddScoped<FinancialItemService>();
        s.AddScoped<FinanceReviewService>();
        s.AddScoped<FinancialsService>();
        s.AddScoped<BudgetService>();
        s.AddScoped<DisbursementService>();
        s.AddScoped<ExecutionService>();
        s.AddScoped<ClosureService>();
        s.AddScoped<ProjectsDashboardService>();

        s.AddScoped<EligibilityGateway>();
        s.AddScoped<IntegrationHandlers>();
        s.AddScoped<ProjectStatusRefresher>();

        s.AddScoped<DonorService>();
        s.AddScoped<CatalogService>();
        s.AddScoped<DonationService>();
        s.AddScoped<CustodyService>();
        s.AddScoped<AgreementService>();
        s.AddScoped<InKindService>();
        s.AddScoped<AdjustmentService>();
        s.AddScoped<ReconciliationService>();
        s.AddScoped<CollectionStatsService>();

        s.AddScoped<AdminService>();
        return s;
    }
}
