using System.ComponentModel.DataAnnotations;

namespace Urban.API.Auth.DTOs;

public record CreateRoleRequest
{
    [Required]
    public string RoleName { get; set; } = string.Empty;
}