using System.ComponentModel.DataAnnotations;

namespace Urban.API.Models;

public class AssignRoleModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string RoleName { get; set; } = string.Empty;
}