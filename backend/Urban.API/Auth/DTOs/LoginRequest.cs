using System.ComponentModel.DataAnnotations;

namespace Urban.API.Auth.DTOs;

public record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}