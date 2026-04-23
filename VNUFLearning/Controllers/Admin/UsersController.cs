using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;
using VNUFLearning.Models.ViewModels;

namespace VNUFLearning.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class UsersController : Controller
    {
        private readonly VnufLearningContext _context;

        public UsersController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Admin/Users")]
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View("~/Views/Admin/Users/Index.cshtml", users);
        }

        [HttpGet]
        [Route("~/Admin/Users/Create")]
        public async Task<IActionResult> Create()
        {
            await LoadRolesAsync();
            return View("~/Views/Admin/Users/Create.cshtml", new UserFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Users/Create")]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadRolesAsync(model.RoleId);
                return View("~/Views/Admin/Users/Create.cshtml", model);
            }

            var studentCode = model.StudentCode.Trim();

            var existedUser = await _context.Users
                .AnyAsync(u => u.StudentCode == studentCode);

            if (existedUser)
            {
                ModelState.AddModelError("StudentCode", "Mã tài khoản đã tồn tại.");
                await LoadRolesAsync(model.RoleId);
                return View("~/Views/Admin/Users/Create.cshtml", model);
            }

            var password = string.IsNullOrWhiteSpace(model.Password)
                ? "123456"
                : model.Password.Trim();

            var user = new User
            {
                StudentCode = studentCode,
                FullName = model.FullName.Trim(),
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                PasswordHash = password,
                RoleId = model.RoleId,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã tạo tài khoản {user.StudentCode} thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("~/Admin/Users/Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Success"] = "Không tìm thấy tài khoản cần sửa.";
                return RedirectToAction(nameof(Index));
            }

            var model = new UserFormViewModel
            {
                UserId = user.UserId,
                StudentCode = user.StudentCode,
                FullName = user.FullName,
                Email = user.Email,
                RoleId = user.RoleId
            };

            await LoadRolesAsync(model.RoleId);
            return View("~/Views/Admin/Users/Edit.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Users/Edit/{id}")]
        public async Task<IActionResult> Edit(int id, UserFormViewModel model)
        {
            if (id != model.UserId)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                await LoadRolesAsync(model.RoleId);
                return View("~/Views/Admin/Users/Edit.cshtml", model);
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Success"] = "Không tìm thấy tài khoản cần sửa.";
                return RedirectToAction(nameof(Index));
            }

            var studentCode = model.StudentCode.Trim();

            var existedUser = await _context.Users
                .AnyAsync(u => u.StudentCode == studentCode && u.UserId != id);

            if (existedUser)
            {
                ModelState.AddModelError("StudentCode", "Mã tài khoản đã tồn tại.");
                await LoadRolesAsync(model.RoleId);
                return View("~/Views/Admin/Users/Edit.cshtml", model);
            }

            user.StudentCode = studentCode;
            user.FullName = model.FullName.Trim();
            user.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            user.RoleId = model.RoleId;

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                user.PasswordHash = model.Password.Trim();
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật tài khoản {user.StudentCode} thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.PasswordHash = "123456";
                _context.Update(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã khôi phục mật khẩu của {user.FullName} về mặc định (123456).";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                if (user.StudentCode == "admin")
                {
                    TempData["Success"] = "Không thể khóa tài khoản admin gốc.";
                    return RedirectToAction(nameof(Index));
                }

                user.IsActive = false;
                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Đã khóa tài khoản {user.StudentCode}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsActive = true;
                _context.Update(user);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Đã mở khóa tài khoản {user.StudentCode}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                if (user.StudentCode == "admin")
                {
                    TempData["Success"] = "Không thể xóa tài khoản admin gốc.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa tài khoản {user.StudentCode} khỏi hệ thống.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadRolesAsync(int? selectedRoleId = null)
        {
            var roles = await _context.Roles
                .OrderBy(r => r.RoleName)
                .ToListAsync();

            ViewBag.RoleId = new SelectList(roles, "RoleId", "RoleName", selectedRoleId);
        }
    }
}