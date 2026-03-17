using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers
{
    public class AccountController : Controller
    {
        private readonly VnufLearningContext _context;

        public AccountController(VnufLearningContext context)
        {
            _context = context;
        }

        // =========================
        // HIỂN THỊ TRANG LOGIN
        // =========================
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }

            return View();
        }

        // =========================
        // XỬ LÝ ĐĂNG NHẬP
        // =========================
        [HttpPost]
        // SỬA LỖI 1: Tham số phải là 'username' để khớp với name="username" của form HTML
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tài khoản và mật khẩu.";
                return View();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u =>
                    u.StudentCode == username &&
                    u.PasswordHash == password);

            if (user == null)
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không chính xác.";
                return View();
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
                ExpiresUtc = DateTime.UtcNow.AddHours(8)
            };

            // =========================
            // SIGN IN
            // =========================
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // Chuyển trang theo Role
            return RedirectToDashboard(user.Role.RoleName);
        }

        // =========================
        // LOGOUT
        // =========================
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Login", "Account");
        }

        // =========================
        // REDIRECT DASHBOARD 
        // =========================
        private IActionResult RedirectToDashboard(string? role = null)
        {
            if (string.IsNullOrEmpty(role))
            {
                role = User.FindFirstValue(ClaimTypes.Role);
            }

            // SỬA LỖI 2 & 3: Bắt đúng tên Role trong SQL và Dùng Redirect cứng 
            // để tránh lỗi trùng tên DashboardController
            switch (role)
            {
                case "Admin":
                    return Redirect("/Admin/Dashboard/Index");

                case "GiangVien": // Tên trong SQL là GiangVien
                    return Redirect("/Teacher/Dashboard/Index");

                case "SinhVien": // Tên trong SQL là SinhVien
                    return Redirect("/Student/Dashboard/Index");

                default:
                    return Redirect("/Home/Index");
            }
        }
    }
}