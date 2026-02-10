using System.ComponentModel.DataAnnotations;

namespace Urban.API.Auth.DTOs;

public record AssignRoleRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string RoleName { get; set; } = string.Empty;
}