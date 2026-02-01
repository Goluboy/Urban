using System.ComponentModel.DataAnnotations;

namespace Urban.API.Models;

public class CreateRoleModel
{
    [Required]
    public string RoleName { get; set; } = string.Empty;
}