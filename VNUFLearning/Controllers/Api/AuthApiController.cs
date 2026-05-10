using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Filters;
using VNUFLearning.Models.ViewModels;
using VNUFLearning.Services;

namespace VNUFLearning.Controllers.Api;

[ApiController]
[Route("api/auth")]
[ApiKeyAuth]
public class AuthApiController : ControllerBase
{
    private readonly VnufLearningContext _context;
    private readonly JwtTokenService _jwt;

    public AuthApiController(VnufLearningContext context, JwtTokenService jwt)
    {
        _context = context;
        _jwt = jwt;
    }

    [HttpGet("healthcheck")]
    public IActionResult Health() => Ok(new { success = true, service = "vnuf-auth", time = DateTime.UtcNow });

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] ApiLoginRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { success = false, message = "Vui lòng nhập đầy đủ tài khoản và mật khẩu." });

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.StudentCode == req.Username);

        if (user is null)
            return Unauthorized(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác." });

        var (matched, needsRehash) = PasswordHasher.Verify(req.Password, user.PasswordHash);
        if (!matched)
            return Unauthorized(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác." });

        if (needsRehash)
        {
            user.PasswordHash = PasswordHasher.Hash(req.Password);
            await _context.SaveChangesAsync();
        }

        var (token, expires) = _jwt.CreateToken(user);
        return Ok(new
        {
            success = true,
            data = new ApiLoginResponse
            {
                AccessToken = token,
                ExpiresAt = expires,
                Role = user.Role?.RoleName ?? string.Empty,
                FullName = user.FullName,
                UserId = user.UserId
            }
        });
    }

    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> ChangePassword([FromBody] ApiChangePasswordRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { success = false, message = "Vui lòng nhập mật khẩu hiện tại và mật khẩu mới." });

        if (req.NewPassword.Length < 6)
            return BadRequest(new { success = false, message = "Mật khẩu mới phải có ít nhất 6 ký tự." });

        if (string.Equals(req.CurrentPassword, req.NewPassword, StringComparison.Ordinal))
            return BadRequest(new { success = false, message = "Mật khẩu mới phải khác mật khẩu hiện tại." });

        var userIdStr = User.FindFirst("UserId")?.Value
                     ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized(new { success = false, message = "Phiên đăng nhập không hợp lệ." });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null)
            return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });

        var (matched, _) = PasswordHasher.Verify(req.CurrentPassword, user.PasswordHash);
        if (!matched)
            return Unauthorized(new { success = false, message = "Mật khẩu hiện tại không đúng." });

        user.PasswordHash = PasswordHasher.Hash(req.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại để lấy token mới." });
    }
}
