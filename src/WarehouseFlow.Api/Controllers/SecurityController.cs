using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/security")]
[Authorize(Policy = PermissionCodes.SecurityManage)]
public sealed class SecurityController(WmsDbContext db) : ControllerBase
{
    [HttpGet("permissions")]
    public async Task<IActionResult> Permissions(CancellationToken cancellationToken) =>
        Ok(await db.Permissions.AsNoTracking().OrderBy(x => x.Module).ThenBy(x => x.Code).ToListAsync(cancellationToken));

    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(x => x.Code).ToListAsync(cancellationToken);
        var links = await db.RolePermissions.AsNoTracking().Include(x => x.Permission).ToListAsync(cancellationToken);
        return Ok(roles.Select(x => new
        {
            x.Code,
            x.Name,
            x.IsActive,
            PermissionCodes = links.Where(p => p.RoleId == x.Id && p.Permission != null).Select(p => p.Permission!.Code).OrderBy(p => p).ToList(),
            PermissionNames = links.Where(p => p.RoleId == x.Id && p.Permission != null).Select(p => p.Permission!.Name).OrderBy(p => p).ToList()
        }));
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "角色编码和名称不能为空" });
        var code = request.Code.Trim();
        if (await db.Roles.AnyAsync(x => x.Code == code, cancellationToken))
            return Conflict(new { message = "角色编码已存在" });
        var permissions = await ResolvePermissions(request.PermissionCodes, cancellationToken);
        if (permissions is null) return BadRequest(new { message = "包含不存在的权限编码" });
        var role = new Role { Code = code, Name = request.Name.Trim() };
        role.RolePermissions.AddRange(permissions.Select(x => new RolePermission { PermissionId = x.Id }));
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { role.Code, role.Name });
    }

    [HttpPut("roles/{code}/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(string code, UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.Include(x => x.RolePermissions).SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (role is null) return NotFound(new { message = "角色不存在" });
        if (role.Code == "Administrator" && request.PermissionCodes.Count != PermissionCodes.All.Count)
            return BadRequest(new { message = "系统管理员角色必须保留全部权限" });
        var permissions = await ResolvePermissions(request.PermissionCodes, cancellationToken);
        if (permissions is null) return BadRequest(new { message = "包含不存在的权限编码" });
        db.RolePermissions.RemoveRange(role.RolePermissions);
        role.RolePermissions = permissions.Select(x => new RolePermission { RoleId = role.Id, PermissionId = x.Id }).ToList();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("roles/{code}")]
    public async Task<IActionResult> UpdateRole(string code, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (role is null) return NotFound(new { message = "角色不存在" });
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "角色名称不能为空" });
        role.Name = request.Name.Trim();
        role.IsActive = request.IsActive;
        if (role.Code == "Administrator" && !request.IsActive) return BadRequest(new { message = "系统管理员角色不能停用" });
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var users = await db.UserAccounts.AsNoTracking().OrderBy(x => x.UserName).ToListAsync(cancellationToken);
        var links = await db.UserRoles.AsNoTracking().Include(x => x.Role).ToListAsync(cancellationToken);
        return Ok(users.Select(x => new
        {
            x.UserName,
            x.DisplayName,
            x.IsActive,
            x.CreatedAt,
            RoleCodes = links.Where(r => r.UserAccountId == x.Id && r.Role != null).Select(r => r.Role!.Code).OrderBy(r => r).ToList(),
            RoleNames = links.Where(r => r.UserAccountId == x.Id && r.Role != null).Select(r => r.Role!.Name).OrderBy(r => r).ToList()
        }));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(new { message = "用户名不能为空，密码至少六位" });
        var userName = request.UserName.Trim();
        if (await db.UserAccounts.AnyAsync(x => x.UserName == userName, cancellationToken))
            return Conflict(new { message = "用户名已存在" });
        var roles = await ResolveRoles(request.RoleCodes, cancellationToken);
        if (roles is null) return BadRequest(new { message = "包含不存在的角色编码" });
        if (roles.Count == 0) return BadRequest(new { message = "用户至少需要一个角色" });
        var user = new UserAccount
        {
            UserName = userName,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? userName : request.DisplayName.Trim(),
            PasswordHash = PasswordHashing.Hash(request.Password)
        };
        user.UserRoles.AddRange(roles.Select(x => new UserRole { RoleId = x.Id }));
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { user.UserName, user.DisplayName });
    }

    [HttpPut("users/{userName}/roles")]
    public async Task<IActionResult> UpdateUserRoles(string userName, UpdateUserRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await db.UserAccounts.Include(x => x.UserRoles).SingleOrDefaultAsync(x => x.UserName == userName, cancellationToken);
        if (user is null) return NotFound(new { message = "用户不存在" });
        var roles = await ResolveRoles(request.RoleCodes, cancellationToken);
        if (roles is null) return BadRequest(new { message = "包含不存在的角色编码" });
        if (roles.Count == 0) return BadRequest(new { message = "用户至少需要一个角色" });
        db.UserRoles.RemoveRange(user.UserRoles);
        user.UserRoles = roles.Select(x => new UserRole { UserAccountId = user.Id, RoleId = x.Id }).ToList();
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("users/{userName}")]
    public async Task<IActionResult> UpdateUser(string userName, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.UserAccounts.SingleOrDefaultAsync(x => x.UserName == userName, cancellationToken);
        if (user is null) return NotFound(new { message = "用户不存在" });
        if (string.IsNullOrWhiteSpace(request.DisplayName)) return BadRequest(new { message = "显示名称不能为空" });
        if (user.UserName == "admin" && !request.IsActive) return BadRequest(new { message = "系统管理员不能停用" });
        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 6) return BadRequest(new { message = "密码至少六位" });
            user.PasswordHash = PasswordHashing.Hash(request.Password);
        }
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<List<Permission>?> ResolvePermissions(IEnumerable<string> codes, CancellationToken cancellationToken)
    {
        var requested = codes.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var permissions = await db.Permissions.Where(x => requested.Contains(x.Code)).ToListAsync(cancellationToken);
        return permissions.Count == requested.Count ? permissions : null;
    }

    private async Task<List<Role>?> ResolveRoles(IEnumerable<string> codes, CancellationToken cancellationToken)
    {
        var requested = codes.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var roles = await db.Roles.Where(x => requested.Contains(x.Code) && x.IsActive).ToListAsync(cancellationToken);
        return roles.Count == requested.Count ? roles : null;
    }
}
