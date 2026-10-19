namespace WarehouseFlow.Api.Contracts;

public sealed record CreateRoleRequest(string Code, string Name, IReadOnlyList<string> PermissionCodes);
public sealed record UpdateRoleRequest(string Name, bool IsActive);
public sealed record UpdateRolePermissionsRequest(IReadOnlyList<string> PermissionCodes);
public sealed record CreateUserRequest(string UserName, string DisplayName, string Password, IReadOnlyList<string> RoleCodes);
public sealed record UpdateUserRequest(string DisplayName, bool IsActive, string? Password);
public sealed record UpdateUserRolesRequest(IReadOnlyList<string> RoleCodes);
