using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers
{
    // Cho phép cả Admin và Giảng viên truy cập
    [Authorize(Roles = "Admin,GiangVien")]
    public class SubjectsController : Controller
    {
        private readonly VnufLearningContext _context;

        public SubjectsController(VnufLearningContext context)
        {
            _context = context;
        }

        [Route("Subjects")]
        [Route("Subjects/Index")]
        public async Task<IActionResult> Index(string keyword)
        {
            // Lấy danh sách môn học kèm theo số lượng câu hỏi
            var query = _context.Subjects.Include(s => s.Questions).AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(s => s.SubjectName.Contains(keyword) || s.Description.Contains(keyword));
            }

            return View(await query.ToListAsync());
        }
    }
}