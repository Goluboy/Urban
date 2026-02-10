using System.ComponentModel.DataAnnotations;

namespace Urban.API.Auth.DTOs;

public record RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    public string? FullName { get; set; }
}