using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Admin
{
    // 1. THÊM DÒNG NÀY VÀO ĐỂ ÉP ĐƯỜNG DẪN CÓ CHỮ ADMIN
    [Route("Admin/[controller]/[action]")]
    public class UsersController : Controller
    {
        private readonly VnufLearningContext _context;

        public UsersController(VnufLearningContext context)
        {
            _context = context;
        }

        // 2. THÊM DÒNG NÀY ĐỂ MẶC ĐỊNH GỌI /Admin/Users LÀ VÀO HÀM NÀY
        [Route("~/Admin/Users")]
        [Route("")]
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                                      .Include(u => u.Role)
                                      .OrderByDescending(u => u.CreatedAt)
                                      .ToListAsync();

            return View("~/Views/admin/Users/Index.cshtml", users);
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
            // 3. Sửa lại hàm chuyển hướng
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa tài khoản {user.StudentCode} khỏi hệ thống.";
            }
            // 4. Sửa lại hàm chuyển hướng
            return RedirectToAction(nameof(Index));
        }
    }
}