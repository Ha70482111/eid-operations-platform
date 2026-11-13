using EidCharity.Domain;

namespace EidCharity.Tests;

/// <summary>Guards the permission catalogue: every role, group and policy refers to a real permission.</summary>
public class PermissionMatrixTests
{
    [Fact]
    public void Permission_keys_are_unique()
    {
        var dupes = Perm.All.GroupBy(p => p.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(dupes);
    }

    [Fact]
    public void Every_permission_belongs_to_a_known_group_and_is_bilingual()
    {
        foreach (var p in Perm.All)
        {
            Assert.True(Perm.Groups.ContainsKey(p.Group), $"{p.Key} has unknown group {p.Group}");
            Assert.False(string.IsNullOrWhiteSpace(p.Ar), p.Key);
            Assert.False(string.IsNullOrWhiteSpace(p.En), p.Key);
        }
    }

    [Fact]
    public void Default_roles_only_use_catalogued_permissions()
    {
        var known = Perm.All.Select(p => p.Key).ToHashSet();
        foreach (var r in DefaultRoles.All)
            Assert.All(r.Permissions, p => Assert.Contains(p, known));
    }

    [Fact]
    public void System_admin_has_every_permission()
    {
        var admin = DefaultRoles.All.Single(r => r.Name == DefaultRoles.SystemAdmin);
        Assert.Equal(Perm.All.Select(p => p.Key).ToHashSet(), admin.Permissions.ToHashSet());
    }

    [Fact]
    public void Collector_cannot_administer_or_approve_finance()
    {
        var collector = DefaultRoles.All.Single(r => r.Name == DefaultRoles.Collector).Permissions.ToHashSet();
        Assert.DoesNotContain(Perm.AdminUsers, collector);
        Assert.DoesNotContain(Perm.AdminRoles, collector);
        Assert.DoesNotContain(Perm.AuditView, collector);
        Assert.Contains(Perm.CollectUse, collector);
    }

    [Fact]
    public void Every_permission_constant_is_catalogued()
    {
        var constants = typeof(Perm).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        var catalogued = Perm.All.Select(p => p.Key).ToHashSet();
        Assert.Empty(constants.Except(catalogued));
    }
}

public class MoneyAndTimeTests
{
    [Theory]
    [InlineData(10.005, 10.01)]
    [InlineData(10.004, 10.00)]
    [InlineData(-2.345, -2.35)]
    public void Money_rounds_half_away_from_zero(decimal input, decimal expected) => Assert.Equal(expected, Money.Round(input));

    [Fact]
    public void Qatar_time_is_utc_plus_three()
    {
        var utc = new DateTime(2026, 3, 1, 21, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 3, 2, 0, 30, 0), QatarTime.ToLocal(utc));
        Assert.Equal(utc, QatarTime.ToUtc(new DateTime(2026, 3, 2, 0, 30, 0)));
    }
}
