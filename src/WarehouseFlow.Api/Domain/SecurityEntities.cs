using System.Security.Cryptography;

namespace WarehouseFlow.Api.Domain;

public static class PermissionCodes
{
    public const string DashboardView = "Dashboard.View";
    public const string InventoryView = "Inventory.View";
    public const string InventoryOperate = "Inventory.Operate";
    public const string InboundOperate = "Inbound.Operate";
    public const string OutboundOperate = "Outbound.Operate";
    public const string MasterDataManage = "MasterData.Manage";
    public const string PlanningOperate = "Planning.Operate";
    public const string QualityOperate = "Quality.Operate";
    public const string ReturnsOperate = "Returns.Operate";
    public const string SecurityManage = "Security.Manage";

    public static IReadOnlyList<string> All { get; } =
    [
        DashboardView, InventoryView, InventoryOperate, InboundOperate, OutboundOperate,
        MasterDataManage, PlanningOperate, QualityOperate, ReturnsOperate, SecurityManage
    ];
}

public sealed record PermissionDefinition(string Code, string Name, string Module);

public static class PermissionCatalog
{
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(PermissionCodes.DashboardView, "仓储总览", "工作台"),
        new(PermissionCodes.InventoryView, "库存查询", "库存管理"),
        new(PermissionCodes.InventoryOperate, "库存作业", "库存管理"),
        new(PermissionCodes.InboundOperate, "入库作业", "仓储作业"),
        new(PermissionCodes.OutboundOperate, "出库作业", "仓储作业"),
        new(PermissionCodes.MasterDataManage, "基础资料维护", "基础资料"),
        new(PermissionCodes.PlanningOperate, "计划协同与单据", "计划协同"),
        new(PermissionCodes.QualityOperate, "收货质检", "质量管理"),
        new(PermissionCodes.ReturnsOperate, "退货管理", "仓储作业"),
        new(PermissionCodes.SecurityManage, "用户角色权限", "系统管理")
    ];
}

public static class SecurityClaims
{
    public const string Permission = "permission";
}

public sealed class UserAccount
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<UserRole> UserRoles { get; set; } = [];
}

public sealed class Role
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<UserRole> UserRoles { get; set; } = [];
    public List<RolePermission> RolePermissions { get; set; } = [];
}

public sealed class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<RolePermission> RolePermissions { get; set; } = [];
}

public sealed class UserRole
{
    public int UserAccountId { get; set; }
    public int RoleId { get; set; }
    public UserAccount? UserAccount { get; set; }
    public Role? Role { get; set; }
}

public sealed class RolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}

public static class PasswordHashing
{
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string encoded)
    {
        try
        {
            var parts = encoded.Split(':', 4);
            if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations)) return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
