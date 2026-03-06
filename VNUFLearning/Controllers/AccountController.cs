using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using VNUFLearning.Data;
using VNUFLearning.Models.ViewModels;


namespace VNUFLearning.Controllers
{
    public class AccountController : Controller
    {
        private readonly VnufLearningContext _context;

        public AccountController(VnufLearningContext context)
        {
            _context = context;
        }

        // Trang Login
        public IActionResult Login()
        {
            return View();
        }

        // Xử lý Login
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
        .Include(u => u.Role)
        .FirstOrDefaultAsync(u =>
            u.StudentCode == model.Username &&
            u.PasswordHash == model.Password);

            if (user == null)
            {
                ViewBag.Error = "Sai tài khoản hoặc mật khẩu";
                return View(model);
            }

            var claims = new List<Claim>
{
    new Claim(ClaimTypes.Name, user.FullName),
    new Claim(ClaimTypes.Role, user.Role.RoleName),
    new Claim("UserId", user.UserId.ToString())
};

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            if (user.Role.RoleName == "Admin")
                return Redirect("/Admin/Dashboard");

            if (user.Role.RoleName == "Teacher")
                return Redirect("/Teacher/Dashboard");

            return Redirect("/Student/Dashboard");
        }
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
  

        }
    }
