using Microsoft.AspNetCore.Identity;

namespace EidCharity.Domain;

/// <summary>Base for all tracked records. Creation/update stamps are filled by the audit interceptor.</summary>
public abstract class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedById { get; set; }
    /// <summary>Optimistic concurrency token (SQL Server rowversion).</summary>
    public byte[]? RowVersion { get; set; }
}

/// <summary>Bilingual name. One record holds both languages; switching language never creates a new record.</summary>
public interface IBilingual
{
    string NameAr { get; set; }
    string NameEn { get; set; }
}

public class DomainException(string messageAr, string messageEn) : Exception(messageEn)
{
    public string MessageAr { get; } = messageAr;
    public string MessageEn { get; } = messageEn;
    public string For(bool arabic) => arabic ? MessageAr : MessageEn;
}

public class ForbiddenException(string permission)
    : DomainException($"ليست لديك صلاحية: {permission}", $"You do not have permission: {permission}")
{
    public string Permission { get; } = permission;
}

public class AppUser : IdentityUser
{
    public string FullNameAr { get; set; } = "";
    public string FullNameEn { get; set; } = "";
    public int? CenterId { get; set; }
    public Center? Center { get; set; }
    public string JobTitle { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string PreferredLanguage { get; set; } = "ar";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class Center : Entity, IBilingual
{
    public string Code { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string DepartmentAr { get; set; } = "";
    public string DepartmentEn { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public static class Money
{
    public const string DefaultCurrency = "QAR";
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

public static class QatarTime
{
    private static readonly TimeZoneInfo Zone = Resolve();
    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Asia/Qatar", "Arab Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Qatar", TimeSpan.FromHours(3), "Qatar", "Qatar");
    }
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public static DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
    /// <summary>UTC start of the Qatar calendar day/month/year containing now.</summary>
    public static DateTime StartOfTodayUtc => ToUtc(Now.Date);
    public static DateTime StartOfMonthUtc => ToUtc(new DateTime(Now.Year, Now.Month, 1));
    public static DateTime StartOfYearUtc => ToUtc(new DateTime(Now.Year, 1, 1));
}
