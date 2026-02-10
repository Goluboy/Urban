using Microsoft.AspNetCore.Identity;

namespace Urban.API.Auth.Services.Interfaces;

public interface IJWTService
{
    string GenerateJwtToken(IdentityUser user);
}