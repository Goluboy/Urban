using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Urban.API.Models;

namespace Urban.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class RoleController(RoleManager<IdentityRole> roleManager, UserManager<IdentityUser> userManager) : ControllerBase
    {
        [HttpPost("create-role")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var roleExists = await roleManager.RoleExistsAsync(model.RoleName);
            if (roleExists)
                return BadRequest(new { message = "Role already exists" });

            var roleResult = await roleManager.CreateAsync(new IdentityRole(model.RoleName));

            if (!roleResult.Succeeded)
                return BadRequest(roleResult.Errors);

            return Ok(new { message = $"Role '{model.RoleName}' created successfully" });
        }

        [HttpPost("assign-role")]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleModel model)
        {
            var user = await userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var result = await userManager.AddToRoleAsync(user, model.RoleName);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = $"Role '{model.RoleName}' assigned to user '{model.Email}' successfully" });
        }
    }
}
