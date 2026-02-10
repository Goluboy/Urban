using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Urban.API.Auth.DTOs;
using Urban.API.Auth.Services.Interfaces;

namespace Urban.API.Auth
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IConfiguration configuration,
        IJWTService jwtService) : ControllerBase
    {
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var user = new IdentityUser { UserName = request.Email, Email = request.Email };    

            var result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = "User registered successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await signInManager.PasswordSignInAsync(request.Email, request.Password, false, false);

            if (!result.Succeeded)
                return Unauthorized(new { message = "Invalid credentials" });


            var user = await userManager.FindByEmailAsync(request.Email);
            var token = jwtService.GenerateJwtToken(user!);

            return Ok(new { token, email = user.Email, id = user.Id });
        }

    }
}
