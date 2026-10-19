using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IConfiguration configuration, WmsDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> CreateToken([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await LoadUser(request.UserName, cancellationToken);
        if (user is null || !PasswordHashing.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "账号或密码不正确" });

        var roles = user.UserRoles.Where(x => x.Role?.IsActive == true).Select(x => x.Role!.Code).Distinct().ToList();
        var permissions = GetPermissions(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("display_name", user.DisplayName)
        };
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        claims.AddRange(permissions.Select(x => new Claim(SecurityClaims.Permission, x)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!));
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims,
            expires: DateTime.UtcNow.AddHours(8), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return Ok(new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            expiresIn = 28800,
            user = new { user.UserName, user.DisplayName, Roles = roles, Permissions = permissions }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await LoadUser(User.Identity?.Name ?? string.Empty, cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(new
        {
            user.UserName,
            user.DisplayName,
            Roles = user.UserRoles.Where(x => x.Role?.IsActive == true).Select(x => x.Role!.Code).Distinct(),
            Permissions = GetPermissions(user)
        });
    }

    private async Task<UserAccount?> LoadUser(string userName, CancellationToken cancellationToken) =>
        await db.UserAccounts
            .AsSplitQuery()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.UserName == userName && x.IsActive, cancellationToken);

    private static List<string> GetPermissions(UserAccount user) => user.UserRoles
        .Where(x => x.Role?.IsActive == true)
        .SelectMany(x => x.Role!.RolePermissions)
        .Where(x => x.Permission?.IsActive == true)
        .Select(x => x.Permission!.Code)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x)
        .ToList();

    public sealed record LoginRequest(string UserName, string Password);
}
