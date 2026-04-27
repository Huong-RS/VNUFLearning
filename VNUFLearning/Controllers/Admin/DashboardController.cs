using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;

namespace VNUFLearning.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class DashboardController : Controller
    {
        private readonly VnufLearningContext _context;

        public DashboardController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Admin")]
        [Route("~/Admin/Dashboard")]
        [Route("~/Admin/Dashboard/Index")]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Dashboard Admin";

            ViewBag.TotalStudents = await _context.Users
                .Include(u => u.Role)
                .CountAsync(u => u.Role.RoleName == "SinhVien");

            ViewBag.TotalTeachers = await _context.Users
                .Include(u => u.Role)
                .CountAsync(u => u.Role.RoleName == "GiangVien");

            ViewBag.TotalUsers = await _context.Users.CountAsync();

            ViewBag.LockedUsers = await _context.Users
                .CountAsync(u => !u.IsActive);

            ViewBag.TotalSubjects = await _context.Subjects
                .CountAsync(s => s.IsActive);

            ViewBag.TotalQuestions = await _context.Questions
                .CountAsync(q => q.IsActive);

            ViewBag.TotalDocuments = await _context.Documents
                .CountAsync(d => d.IsActive);

            ViewBag.TotalExamResults = await _context.ExamResults.CountAsync();

            ViewBag.NewUsersThisWeek = await _context.Users
                .CountAsync(u => u.CreatedAt >= DateTime.Now.AddDays(-7));

            ViewBag.NewQuestionsThisWeek = await _context.Questions
                .CountAsync(q => q.CreatedAt >= DateTime.Now.AddDays(-7));

            ViewBag.NewDocumentsThisWeek = await _context.Documents
                .CountAsync(d => d.IsActive && d.CreatedAt >= DateTime.Now.AddDays(-7));

            return View("~/Views/Admin/Dashboard/Index.cshtml");
        }
    }
}