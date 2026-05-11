using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VNUFLearning.Data;
using VNUFLearning.Models;
using VNUFLearning.Models.ViewModels;
using VNUFLearning.Services;

namespace VNUFLearning.Controllers
{
    public class AccountController : Controller
    {
        private readonly VnufLearningContext _context;
        private readonly JwtTokenService _jwt;

        public AccountController(VnufLearningContext context, JwtTokenService jwt)
        {
            _context = context;
            _jwt = jwt;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            username = (username ?? string.Empty).Trim();
            password = (password ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ tài khoản và mật khẩu.";
                return RedirectToAction("Login");
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.StudentCode == username);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Tài khoản hoặc mật khẩu không chính xác.";
                return RedirectToAction(nameof(Login));
            }

            var (matched, needsRehash) = PasswordHasher.Verify(password, user.PasswordHash);
            if (!matched)
            {
                TempData["ErrorMessage"] = "Tài khoản hoặc mật khẩu không chính xác.";
                return RedirectToAction(nameof(Login));
            }

            // Auto-migrate: nếu mật khẩu cũ là plain-text, hash lại bằng BCrypt
            if (needsRehash)
            {
                user.PasswordHash = PasswordHasher.Hash(password);
                await _context.SaveChangesAsync();
            }

            // =========================
            // TẠO CLAIMS
            // =========================
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.StudentCode),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role.RoleName),
                new Claim("UserId", user.UserId.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
            };

            // =========================
            // SIGN IN
            // =========================
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // Phát hành JWT vào cookie để Gateway có thể validate khi đứng trước
            var (jwtToken, expiresAt) = _jwt.CreateToken(user);
            Response.Cookies.Append("access_token", jwtToken, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = expiresAt,
                Secure = Request.IsHttps
            });

            // Chuyển trang theo Role
            return RedirectToDashboard(user.Role.RoleName);
        }

        // =========================
        // ĐỔI MẬT KHẨU - GET
        // =========================
        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

        // =========================
        // ĐỔI MẬT KHẨU - POST
        // =========================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var userIdStr = User.FindFirstValue("UserId");
            if (!int.TryParse(userIdStr, out var userId))
            {
                ModelState.AddModelError(string.Empty, "Phiên đăng nhập không hợp lệ.");
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản.");
                return View(model);
            }

            var (matched, _) = PasswordHasher.Verify(model.CurrentPassword, user.PasswordHash);
            if (!matched)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
                return View(model);
            }

            if (string.Equals(model.CurrentPassword, model.NewPassword, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới phải khác mật khẩu hiện tại.");
                return View(model);
            }

            user.PasswordHash = PasswordHasher.Hash(model.NewPassword);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.";

            // Buộc đăng nhập lại để invalidate session cũ
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("access_token");
            return RedirectToAction(nameof(Login));
        }

        // =========================
        // LOGOUT
        // =========================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            Response.Cookies.Delete("access_token");

            return RedirectToAction("Login", "Account");
        }

        private IActionResult RedirectToDashboard(string? role = null)
        {
            if (string.IsNullOrEmpty(role))
            {
                role = User.FindFirstValue(ClaimTypes.Role);
            }

            return role switch
            {
                "Admin" => Redirect("/Admin/Dashboard"),
                "GiangVien" => Redirect("/Teacher/Dashboard"),
                "SinhVien" => Redirect("/Student/Dashboard"),
                _ => RedirectToAction("Login", "Account")
            };
        }
    }
}
