using System.ComponentModel.DataAnnotations;

namespace Urban.API.Models;

public class RegisterModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    public string? FullName { get; set; }
}