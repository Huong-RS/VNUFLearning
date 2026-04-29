using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;

namespace VNUFLearning.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class StatisticsController : Controller
    {
        private readonly VnufLearningContext _context;

        public StatisticsController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Admin/Statistics")]
        public async Task<IActionResult> Index()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalStudents = await _context.Users.CountAsync(u => u.Role.RoleName == "SinhVien");
            var totalTeachers = await _context.Users.CountAsync(u => u.Role.RoleName == "GiangVien");

            var totalSubmissions = await _context.ExamResults.CountAsync();
            var totalDocuments = await _context.Documents.CountAsync();
            var totalSubjects = await _context.Subjects.CountAsync();
            var totalQuestions = await _context.Questions.CountAsync();
            var totalBlocked = await _context.Users.CountAsync(u => !u.IsActive);

            var mcqCount = await _context.Questions.CountAsync(q => q.QuestionType == 1);
            var essayCount = await _context.Questions.CountAsync(q => q.QuestionType == 2);

            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-6 + i))
                .ToList();

            var examResults = await _context.ExamResults
                .Where(r => r.StartedAt != null && r.StartedAt >= DateTime.Today.AddDays(-6))
                .ToListAsync();

            var chartLabels = last7Days.Select(d => d.ToString("dd/MM")).ToList();

            var chartData = last7Days
                .Select(d => examResults.Count(r => r.StartedAt?.Date == d.Date))
                .ToList();

            var topSubjects = await _context.Subjects
                .Select(s => new
                {
                    s.SubjectName,
                    QuestionCount = s.Questions.Count(),
                    SubmitCount = s.ExamResults.Count()
                })
                .OrderByDescending(x => x.SubmitCount)
                .ThenByDescending(x => x.QuestionCount)
                .Take(5)
                .ToListAsync();

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalTeachers = totalTeachers;
            ViewBag.TotalSubmissions = totalSubmissions;
            ViewBag.TotalDocuments = totalDocuments;
            ViewBag.TotalSubjects = totalSubjects;
            ViewBag.TotalQuestions = totalQuestions;
            ViewBag.TotalBlocked = totalBlocked;

            ViewBag.McqCount = mcqCount;
            ViewBag.EssayCount = essayCount;

            ViewBag.ChartLabels = chartLabels;
            ViewBag.ChartData = chartData;
            ViewBag.TopSubjects = topSubjects;

            return View("~/Views/Admin/Statistics/Index.cshtml");
        }
    }
}