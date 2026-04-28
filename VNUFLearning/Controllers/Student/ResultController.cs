using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;

namespace VNUFLearning.Controllers.Student
{
    [Route("Student/[controller]")]
    public class ResultController : Controller
    {
        private readonly VnufLearningContext _context;

        public ResultController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var results = await _context.ExamResults
                .Include(r => r.Subject)
                .Include(r => r.Exam)
                .OrderByDescending(r => r.StartedAt)
                .ToListAsync();

            return View("~/Views/Student/Result/Index.cshtml", results);
        }

        [HttpGet("Detail/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var result = await _context.ExamResults
                .Include(r => r.Subject)
                .Include(r => r.Exam)
                .Include(r => r.ExamDetails)
                    .ThenInclude(d => d.Question)
                .FirstOrDefaultAsync(r => r.ExamResultId == id);

            if (result == null)
                return NotFound();

            return View("~/Views/Student/Result/Detail.cshtml", result);
        }
    }
}