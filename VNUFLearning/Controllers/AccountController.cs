using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VNUFLearning.Data;

namespace VNUFLearning.Controllers
{
    public class AccountController : Controller
    {
        private readonly VnufLearningContext _context;

        public AccountController(VnufLearningContext context)
        {
            _context = context;
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

            try
            {
                var user = await _context.Users
     .AsNoTracking()
     .Where(u => u.StudentCode != null && u.StudentCode.Trim() == username)
     .Select(u => new
     {
         u.UserId,
         u.StudentCode,
         u.PasswordHash,
         u.FullName,
         u.IsActive,
         RoleName = u.Role.RoleName
     })
     .FirstOrDefaultAsync();

                if (user == null)
                {
                    TempData["ErrorMessage"] = "Tài khoản không tồn tại.";
                    return RedirectToAction("Login");
                }
                if (!user.IsActive)
                {
                    TempData["ErrorMessage"] = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.";
                    return RedirectToAction("Login");
                }

                var dbPassword = (user.PasswordHash ?? string.Empty).Trim();

                // Sau khi ổn sẽ đổi sang BCrypt.
                if (dbPassword != password)
                {
                    TempData["ErrorMessage"] = "Mật khẩu không chính xác.";
                    return RedirectToAction("Login");
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.StudentCode ?? username),
                    new Claim("FullName", string.IsNullOrWhiteSpace(user.FullName) ? user.StudentCode ?? username : user.FullName),
                    new Claim(ClaimTypes.Role, user.RoleName ?? ""),
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

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties
                );

                return RedirectToDashboard(user.RoleName);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Không thể kết nối cơ sở dữ liệu. Vui lòng thử lại sau.";
                return RedirectToAction("Login");
            }
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

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