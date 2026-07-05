namespace Erp.Application.DTOs;

public class CompanyMembershipDto
{
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string? RoleId { get; set; }
    public string? RoleName { get; set; }
}
