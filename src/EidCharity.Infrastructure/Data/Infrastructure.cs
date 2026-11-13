using System.Text.Json;
using EidCharity.Application;
using EidCharity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace EidCharity.Infrastructure.Data;

/// <summary>
/// Stamps created/updated fields, writes a field-level change log for tracked business entities,
/// and makes the audit log append-only (no update/delete from any code path that goes through EF).
/// </summary>
public class AuditInterceptor(ICurrentUser user) : SaveChangesInterceptor
{
    private static readonly HashSet<string> Ignored = [nameof(Entity.RowVersion), nameof(Entity.UpdatedAt), nameof(Entity.UpdatedById)];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is { } ctx) Apply(ctx);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } ctx) Apply(ctx);
        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext ctx)
    {
        var now = DateTime.UtcNow;
        string? uid; string uname;
        try { uid = user.UserId; uname = user.UserName; } catch { uid = null; uname = "system"; }

        foreach (var e in ctx.ChangeTracker.Entries<AuditLog>())
            if (e.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Audit log is append-only.");

        var logs = new List<AuditLog>();
        foreach (var e in ctx.ChangeTracker.Entries<Entity>().ToList())
        {
            switch (e.State)
            {
                case EntityState.Added:
                    e.Entity.CreatedAt = now; e.Entity.CreatedById ??= uid;
                    break;
                case EntityState.Modified:
                    e.Entity.UpdatedAt = now; e.Entity.UpdatedById = uid;
                    var changed = e.Properties.Where(p => p.IsModified && !Ignored.Contains(p.Metadata.Name) && !Equals(p.OriginalValue, p.CurrentValue)).ToList();
                    if (changed.Count > 0)
                        logs.Add(Log(e, "Updated", changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue), changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue), now, uid, uname));
                    break;
                case EntityState.Deleted:
                    logs.Add(Log(e, "Deleted", e.Properties.Where(p => !Ignored.Contains(p.Metadata.Name)).ToDictionary(p => p.Metadata.Name, p => p.OriginalValue), null, now, uid, uname));
                    break;
            }
        }
        if (logs.Count > 0) ctx.Set<AuditLog>().AddRange(logs);
    }

    private static AuditLog Log(EntityEntry<Entity> e, string action, Dictionary<string, object?>? old, Dictionary<string, object?>? @new, DateTime now, string? uid, string uname) => new()
    {
        At = now, UserId = uid, UserName = uname, Module = "Data", Action = action, EntityType = e.Entity.GetType().Name, EntityId = e.Entity.Id.ToString(),
        OldValues = old is null ? null : JsonSerializer.Serialize(old), NewValues = @new is null ? null : JsonSerializer.Serialize(@new),
    };
}

/// <summary>Concurrency-safe numbering (UPDLOCK row per key).</summary>
public class SqlNumberGenerator(EidDbContext db) : INumberGenerator
{
    public async Task<string> NextAsync(string key, string prefix, int width = 6, bool withYear = true, CancellationToken ct = default)
    {
        var year = QatarTime.Now.Year;
        var fullKey = withYear ? $"{key}:{year}" : key;
        const string sql = """
            DECLARE @v BIGINT;
            UPDATE [admin].[NumberSequences] WITH (UPDLOCK, ROWLOCK) SET @v = [Next] = [Next] + 1 WHERE [Key] = {0};
            IF @@ROWCOUNT = 0
            BEGIN
                BEGIN TRY
                    INSERT INTO [admin].[NumberSequences]([Key],[Next]) VALUES ({0}, 1); SET @v = 1;
                END TRY
                BEGIN CATCH
                    UPDATE [admin].[NumberSequences] WITH (UPDLOCK, ROWLOCK) SET @v = [Next] = [Next] + 1 WHERE [Key] = {0};
                END CATCH
            END
            SELECT @v AS [Value];
            """;
        var v = (await db.Database.SqlQueryRaw<long>(sql, fullKey).ToListAsync(ct)).Single();
        return withYear ? $"{prefix}-{year}-{v.ToString().PadLeft(width, '0')}" : $"{prefix}-{v.ToString().PadLeft(width, '0')}";
    }
}

/// <summary>sp_getapplock bound to the current transaction: concurrent confirmations on the same cap are serialised.</summary>
public class SqlResourceLock(EidDbContext db) : IResourceLock
{
    public async Task AcquireAsync(string resource, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Resource locks require an active transaction.");
        var rc = (await db.Database.SqlQueryRaw<int>(
            "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; SELECT @r AS [Value];",
            resource).ToListAsync(ct)).Single();
        if (rc < 0) throw new DomainException("النظام مشغول بعملية متزامنة على نفس السقف، حاول مرة أخرى.", "A concurrent operation is in progress on the same cap. Please retry.");
    }
}

public class LocalFileStore(IConfiguration config) : IFileStore
{
    private string Root => config["Storage:Root"] ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var dir = Path.Combine(Root, DateTime.UtcNow.ToString("yyyy/MM"));
        Directory.CreateDirectory(dir);
        var safe = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var full = Path.Combine(dir, safe);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return Path.GetRelativePath(Root, full);
    }

    public Task<Stream?> OpenAsync(string storagePath, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(Path.Combine(Root, storagePath));
        if (!full.StartsWith(Path.GetFullPath(Root), StringComparison.Ordinal) || !File.Exists(full)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(full));
    }
}

/// <summary>Used by background jobs and the seeder: acts as the system with full permissions.</summary>
public class SystemCurrentUser : ICurrentUser
{
    public static readonly SystemCurrentUser Instance = new();
    private static readonly IReadOnlySet<string> All = Perm.All.Select(p => p.Key).ToHashSet();
    public string? UserId { get; set; }
    public string UserName { get; set; } = "system";
    public string DisplayName { get; set; } = "النظام";
    public int? CenterId { get; set; }
    public bool IsAuthenticated => true;
    public bool Has(string permission) => true;
    public IReadOnlySet<string> Permissions => All;
}
