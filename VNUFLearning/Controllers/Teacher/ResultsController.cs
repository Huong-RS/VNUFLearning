using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;

namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]
    [Route("Teacher/[controller]/[action]")]
    public class ResultsController : Controller
    {
        private readonly VnufLearningContext _context;

        public ResultsController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Teacher/Results")]
        public async Task<IActionResult> Index(int examId, string? keyword)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var exam = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.ExamQuestions)
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.TeacherId == teacherId.Value);

            if (exam == null) return NotFound();

            var query = _context.ExamResults
                .Include(r => r.User)
                .Include(r => r.ExamDetails)
                    .ThenInclude(d => d.Question)
                .Where(r => r.ExamId == examId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(r =>
                    r.User.FullName.Contains(keyword) ||
                    r.User.StudentCode.Contains(keyword));
            }

            var results = await query
                .OrderByDescending(r => r.FinishedAt)
                .ToListAsync();

            ViewBag.Exam = exam;
            ViewBag.Keyword = keyword ?? "";

            ViewBag.TotalSubmitted = results.Count;
            ViewBag.AverageScore = results.Any(x => x.Score.HasValue)
                ? results.Where(x => x.Score.HasValue).Average(x => x.Score!.Value)
                : 0;

            ViewBag.PassRate = results.Any(x => x.Score.HasValue)
                ? results.Where(x => x.Score >= 5).Count() * 100.0 / results.Where(x => x.Score.HasValue).Count()
                : 0;

            ViewBag.AiPending = results.Count(x => x.Status == "AI_DANG_CHAM");

            return View("~/Views/Teacher/Results/Index.cshtml", results);
        }

        private int? GetTeacherId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : null;
        }
    }
}