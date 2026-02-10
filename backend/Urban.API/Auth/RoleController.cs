using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Urban.API.Auth.DTOs;

namespace Urban.API.Auth
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class RoleController(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager) : ControllerBase
    {
        [HttpPost("create-role")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var roleExists = await roleManager.RoleExistsAsync(request.RoleName);
            if (roleExists)
                return BadRequest(new { message = "Role already exists" });

            var roleResult = await roleManager.CreateAsync(new IdentityRole(request.RoleName));

            if (!roleResult.Succeeded)
                return BadRequest(roleResult.Errors);

            return Ok(new { message = $"Role '{request.RoleName}' created successfully" });
        }

        [HttpPost("assign-role")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var result = await userManager.AddToRoleAsync(user, request.RoleName);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = $"Role '{request.RoleName}' assigned to user '{request.Email}' successfully" });
        }
    }
}
