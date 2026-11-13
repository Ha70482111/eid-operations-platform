using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EidCharity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "col");

            migrationBuilder.EnsureSchema(
                name: "admin");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "prj");

            migrationBuilder.EnsureSchema(
                name: "integ");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "ApprovalLimits",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Module = table.Column<int>(type: "int", nullable: false),
                    CenterId = table.Column<int>(type: "int", nullable: true),
                    ExpenseType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalLimits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Module = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Beneficiaries",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UniqueCount = table.Column<int>(type: "int", nullable: false),
                    Occurrences = table.Column<int>(type: "int", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beneficiaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetAdjustments",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    FromLineId = table.Column<int>(type: "int", nullable: true),
                    ToLineId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetAdjustments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Centers",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DepartmentAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DepartmentEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Centers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Closures",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ExecutionDocumented = table.Column<bool>(type: "bit", nullable: false),
                    ExpensesSettled = table.Column<bool>(type: "bit", nullable: false),
                    OpenRequestsResolved = table.Column<bool>(type: "bit", nullable: false),
                    PledgesResolved = table.Column<bool>(type: "bit", nullable: false),
                    DifferencesSettled = table.Column<bool>(type: "bit", nullable: false),
                    FinalReportApproved = table.Column<bool>(type: "bit", nullable: false),
                    BalanceTreatment = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Closures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionResults",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    FinancialItemId = table.Column<int>(type: "int", nullable: true),
                    LicenseId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TransactionNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AgreementNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    MappingVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectorPreferences",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectorPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Custodies",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CollectorUserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CollectorName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HandedOverAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DifferenceReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    HandoverType = table.Column<int>(type: "int", nullable: true),
                    DepositReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    HandedOverAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Custodies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Delegations",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromUserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ToUserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Module = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Delegations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonorReports",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DonorName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Responsible = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonorReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Donors",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    NationalId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    OrganizationType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    MarketingConsent = table.Column<bool>(type: "bit", nullable: false),
                    Privacy = table.Column<int>(type: "int", nullable: false),
                    MergedIntoDonorId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionMilestones",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PlannedStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Responsible = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Indicator = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DelayReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CorrectiveAction = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionMilestones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancialItems",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    GlMapping = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inbox",
                schema: "integ",
                columns: table => new
                {
                    IdempotencyKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => x.IdempotencyKey);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Permission = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CenterId = table.Column<int>(type: "int", nullable: true),
                    TitleAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DedupKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                schema: "admin",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Next = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "integ",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCatalog",
                schema: "col",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ProgramId = table.Column<int>(type: "int", nullable: false),
                    CenterId = table.Column<int>(type: "int", nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProgramCode = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProgramNameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProgramNameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CenterNameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CenterNameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    FinancialItemNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    FinancialItemNameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Visual = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    QuickAmounts = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CollectionCap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    UnavailableReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    MappingVersion = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCatalog", x => x.ProjectId);
                });

            migrationBuilder.CreateTable(
                name: "ReallocationRequests",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DonationLineId = table.Column<int>(type: "int", nullable: false),
                    FromProjectId = table.Column<int>(type: "int", nullable: false),
                    ToProjectId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DonorConsentObtained = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DecidedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReallocationRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptCorrections",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CorrectedDonorNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DecidedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptCorrections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationItems",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    DonationId = table.Column<int>(type: "int", nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Responsible = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleProfiles",
                schema: "admin",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleProfiles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                schema: "admin",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Programs",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CenterId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ApprovedBudget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnallocatedReserve = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsStandaloneWrapper = table.Column<bool>(type: "bit", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Programs_Centers_CenterId",
                        column: x => x.CenterId,
                        principalSchema: "admin",
                        principalTable: "Centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    FullNameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    FullNameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CenterId = table.Column<int>(type: "int", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Centers_CenterId",
                        column: x => x.CenterId,
                        principalSchema: "admin",
                        principalTable: "Centers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Agreements",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DonorId = table.Column<int>(type: "int", nullable: false),
                    RepresentativeId = table.Column<int>(type: "int", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false),
                    ReportingTerms = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    AmendmentReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ApprovedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedCopyReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agreements_Donors_DonorId",
                        column: x => x.DonorId,
                        principalSchema: "col",
                        principalTable: "Donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Donations",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DonorId = table.Column<int>(type: "int", nullable: true),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ChannelFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectorUserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CollectorName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CenterId = table.Column<int>(type: "int", nullable: false),
                    CollectionPoint = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PosTerminal = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PosResult = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Settlement = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    AgreementId = table.Column<int>(type: "int", nullable: true),
                    InstallmentId = table.Column<int>(type: "int", nullable: true),
                    CustodyId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Donations_Donors_DonorId",
                        column: x => x.DonorId,
                        principalSchema: "col",
                        principalTable: "Donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DonorRepresentatives",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonorId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CanSign = table.Column<bool>(type: "bit", nullable: false),
                    AuthorizationReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonorRepresentatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonorRepresentatives_Donors_DonorId",
                        column: x => x.DonorId,
                        principalSchema: "col",
                        principalTable: "Donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InKindDonations",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DonorId = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ProjectNameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ItemDescription = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ReceivedBy = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StorageNotes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValuationBasis = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ValuationApproved = table.Column<bool>(type: "bit", nullable: false),
                    DistributedQuantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InKindDonations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InKindDonations_Donors_DonorId",
                        column: x => x.DonorId,
                        principalSchema: "col",
                        principalTable: "Donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pledges",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DonorId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FulfilledAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pledges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pledges_Donors_DonorId",
                        column: x => x.DonorId,
                        principalSchema: "col",
                        principalTable: "Donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Licenses",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    CollectionFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CollectionTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    ProgramId = table.Column<int>(type: "int", nullable: true),
                    Cap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Channels = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Purposes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Conditions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsSuspended = table.Column<bool>(type: "bit", nullable: false),
                    SuspensionReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PreviousLicenseId = table.Column<int>(type: "int", nullable: true),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    IsSuperseded = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Licenses_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "prj",
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProgramId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SummaryAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SummaryEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Objectives = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Activities = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ExecutionScope = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CenterId = table.Column<int>(type: "int", nullable: false),
                    FollowUpUserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ExecutionStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ExecutionEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    BeneficiaryCategory = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TargetBeneficiaries = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryUnit = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ExecutionCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectionCap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StudyReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Visual = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    QuickAmounts = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    CollectionStatus = table.Column<int>(type: "int", nullable: false),
                    ExecutionStatus = table.Column<int>(type: "int", nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    MappingVersion = table.Column<int>(type: "int", nullable: false),
                    MappingSyncStatus = table.Column<int>(type: "int", nullable: false),
                    MappingSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MappingSyncError = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReviewerId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ApprovedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PendingChangesJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Centers_CenterId",
                        column: x => x.CenterId,
                        principalSchema: "admin",
                        principalTable: "Centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Projects_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalSchema: "prj",
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "identity",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgreementLines",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgreementId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ProjectNameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProjectNameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgreementLines_Agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalSchema: "col",
                        principalTable: "Agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Installments",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgreementId = table.Column<int>(type: "int", nullable: false),
                    Seq = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PreviousDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RescheduleReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Installments_Agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalSchema: "col",
                        principalTable: "Agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DonationLines",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonationId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ProgramId = table.Column<int>(type: "int", nullable: false),
                    FinancialItemId = table.Column<int>(type: "int", nullable: false),
                    LicenseId = table.Column<int>(type: "int", nullable: false),
                    MappingVersion = table.Column<int>(type: "int", nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProjectNameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProjectNameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    FinancialItemNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReallocatedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Restrictions = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonationLines_Donations_DonationId",
                        column: x => x.DonationId,
                        principalSchema: "col",
                        principalTable: "Donations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DonationId = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReplacesReceiptId = table.Column<int>(type: "int", nullable: true),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AgreementNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_Donations_DonationId",
                        column: x => x.DonationId,
                        principalSchema: "col",
                        principalTable: "Donations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundRequests",
                schema: "col",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DonationId = table.Column<int>(type: "int", nullable: false),
                    DonationLineId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsChargeback = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DecidedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ExecutionReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundRequests_Donations_DonationId",
                        column: x => x.DonationId,
                        principalSchema: "col",
                        principalTable: "Donations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLines",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "prj",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectLicenseMappings",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    FinancialItemId = table.Column<int>(type: "int", nullable: true),
                    LicenseId = table.Column<int>(type: "int", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    LinkCap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MappingVersion = table.Column<int>(type: "int", nullable: false),
                    ScopeNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLicenseMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLicenseMappings_FinancialItems_FinancialItemId",
                        column: x => x.FinancialItemId,
                        principalSchema: "prj",
                        principalTable: "FinancialItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLicenseMappings_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "prj",
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectLicenseMappings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "prj",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectReviews",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ByUserName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectReviews_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "prj",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisbursementRequests",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    BudgetLineId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PayeeName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PayeeDetails = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ExpenseType = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsAdvance = table.Column<bool>(type: "bit", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InvoiceReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SpecialistReviewed = table.Column<bool>(type: "bit", nullable: false),
                    FinanceChecked = table.Column<bool>(type: "bit", nullable: false),
                    ReservedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExecutedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReleasedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ApprovedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisbursementRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisbursementRequests_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalSchema: "prj",
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisbursementRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "prj",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisbursementActions",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Step = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ByUserName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisbursementActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisbursementActions_DisbursementRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "prj",
                        principalTable: "DisbursementRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisbursementPayments",
                schema: "prj",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DocumentReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisbursementPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisbursementPayments_DisbursementRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "prj",
                        principalTable: "DisbursementRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgreementLines_AgreementId",
                schema: "col",
                table: "AgreementLines",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_DonorId",
                schema: "col",
                table: "Agreements",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_Number",
                schema: "col",
                table: "Agreements",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_OwnerType_OwnerId",
                schema: "admin",
                table: "Attachments",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_At",
                schema: "audit",
                table: "AuditLogs",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                schema: "audit",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_ProjectId",
                schema: "prj",
                table: "BudgetLines",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Centers_Code",
                schema: "admin",
                table: "Centers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Closures_ProjectId",
                schema: "prj",
                table: "Closures",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionResults_IdempotencyKey",
                schema: "prj",
                table: "CollectionResults",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionResults_ProjectId_Kind",
                schema: "prj",
                table: "CollectionResults",
                columns: new[] { "ProjectId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectorPreferences_UserId_ProjectId",
                schema: "col",
                table: "CollectorPreferences",
                columns: new[] { "UserId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Custodies_CollectorUserId_Status",
                schema: "col",
                table: "Custodies",
                columns: new[] { "CollectorUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementActions_RequestId",
                schema: "prj",
                table: "DisbursementActions",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementPayments_Reference",
                schema: "prj",
                table: "DisbursementPayments",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementPayments_RequestId",
                schema: "prj",
                table: "DisbursementPayments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementRequests_BudgetLineId",
                schema: "prj",
                table: "DisbursementRequests",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementRequests_Number",
                schema: "prj",
                table: "DisbursementRequests",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementRequests_ProjectId",
                schema: "prj",
                table: "DisbursementRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DonationLines_DonationId",
                schema: "col",
                table: "DonationLines",
                column: "DonationId");

            migrationBuilder.CreateIndex(
                name: "IX_DonationLines_LicenseId_ProjectId",
                schema: "col",
                table: "DonationLines",
                columns: new[] { "LicenseId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_DonationLines_ProjectId",
                schema: "col",
                table: "DonationLines",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_CollectorUserId_Status_ConfirmedAt",
                schema: "col",
                table: "Donations",
                columns: new[] { "CollectorUserId", "Status", "ConfirmedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_DonorId",
                schema: "col",
                table: "Donations",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_IdempotencyKey",
                schema: "col",
                table: "Donations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donations_Number",
                schema: "col",
                table: "Donations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonorRepresentatives_DonorId",
                schema: "col",
                table: "DonorRepresentatives",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_Donors_Email",
                schema: "col",
                table: "Donors",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Donors_NameAr",
                schema: "col",
                table: "Donors",
                column: "NameAr");

            migrationBuilder.CreateIndex(
                name: "IX_Donors_Number",
                schema: "col",
                table: "Donors",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donors_Phone",
                schema: "col",
                table: "Donors",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialItems_Number",
                schema: "prj",
                table: "FinancialItems",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InKindDonations_DonorId",
                schema: "col",
                table: "InKindDonations",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_AgreementId",
                schema: "col",
                table: "Installments",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_Number",
                schema: "prj",
                table: "Licenses",
                column: "Number");

            migrationBuilder.CreateIndex(
                name: "IX_Licenses_ProgramId",
                schema: "prj",
                table: "Licenses",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DedupKey",
                schema: "admin",
                table: "Notifications",
                column: "DedupKey");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                schema: "admin",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_IdempotencyKey",
                schema: "integ",
                table: "Outbox",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_State_NextAttemptAt",
                schema: "integ",
                table: "Outbox",
                columns: new[] { "State", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Pledges_DonorId",
                schema: "col",
                table: "Pledges",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_CenterId",
                schema: "prj",
                table: "Programs",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_Code",
                schema: "prj",
                table: "Programs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLicenseMappings_FinancialItemId",
                schema: "prj",
                table: "ProjectLicenseMappings",
                column: "FinancialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLicenseMappings_LicenseId",
                schema: "prj",
                table: "ProjectLicenseMappings",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLicenseMappings_ProjectId",
                schema: "prj",
                table: "ProjectLicenseMappings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReviews_ProjectId",
                schema: "prj",
                table: "ProjectReviews",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ApprovalStatus_CenterId",
                schema: "prj",
                table: "Projects",
                columns: new[] { "ApprovalStatus", "CenterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CenterId",
                schema: "prj",
                table: "Projects",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Code",
                schema: "prj",
                table: "Projects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProgramId",
                schema: "prj",
                table: "Projects",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_DonationId",
                schema: "col",
                table: "Receipts",
                column: "DonationId",
                unique: true,
                filter: "[IsCancelled] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_Number",
                schema: "col",
                table: "Receipts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequests_DonationId",
                schema: "col",
                table: "RefundRequests",
                column: "DonationId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "identity",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_Permission",
                schema: "admin",
                table: "RolePermissions",
                columns: new[] { "RoleId", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "identity",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "identity",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "identity",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "identity",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CenterId",
                schema: "identity",
                table: "Users",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgreementLines",
                schema: "col");

            migrationBuilder.DropTable(
                name: "ApprovalLimits",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "Attachments",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "Beneficiaries",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "BudgetAdjustments",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "Closures",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "CollectionResults",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "CollectorPreferences",
                schema: "col");

            migrationBuilder.DropTable(
                name: "Custodies",
                schema: "col");

            migrationBuilder.DropTable(
                name: "Delegations",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "DisbursementActions",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "DisbursementPayments",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "DonationLines",
                schema: "col");

            migrationBuilder.DropTable(
                name: "DonorReports",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "DonorRepresentatives",
                schema: "col");

            migrationBuilder.DropTable(
                name: "ExecutionMilestones",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "Inbox",
                schema: "integ");

            migrationBuilder.DropTable(
                name: "InKindDonations",
                schema: "col");

            migrationBuilder.DropTable(
                name: "Installments",
                schema: "col");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "NumberSequences",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "integ");

            migrationBuilder.DropTable(
                name: "Pledges",
                schema: "col");

            migrationBuilder.DropTable(
                name: "ProjectCatalog",
                schema: "col");

            migrationBuilder.DropTable(
                name: "ProjectLicenseMappings",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "ProjectReviews",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "ReallocationRequests",
                schema: "col");

            migrationBuilder.DropTable(
                name: "ReceiptCorrections",
                schema: "col");

            migrationBuilder.DropTable(
                name: "Receipts",
                schema: "col");

            migrationBuilder.DropTable(
                name: "ReconciliationItems",
                schema: "col");

            migrationBuilder.DropTable(
                name: "RefundRequests",
                schema: "col");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "RolePermissions",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "RoleProfiles",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "Settings",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "DisbursementRequests",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "Agreements",
                schema: "col");

            migrationBuilder.DropTable(
                name: "FinancialItems",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "Licenses",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "Donations",
                schema: "col");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "BudgetLines",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "Donors",
                schema: "col");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "Programs",
                schema: "prj");

            migrationBuilder.DropTable(
                name: "Centers",
                schema: "admin");
        }
    }
}
