using Microsoft.AspNetCore.Identity;

namespace Urban.API.Services.Interfaces;

public interface IJWTService
{
    string GenerateJwtToken(IdentityUser user);
}