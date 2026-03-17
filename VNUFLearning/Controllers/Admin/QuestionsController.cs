using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers
{
    [Authorize(Roles = "Admin,GiangVien")]
    public class QuestionsController : Controller
    {
        private readonly VnufLearningContext _context;

        public QuestionsController(VnufLearningContext context)
        {
            _context = context;
        }

        [Route("Questions")]
        [Route("Questions/Index")]
        public async Task<IActionResult> Index(string keyword, int? subjectId)
        {
            // Truyền danh sách môn học ra View để làm thẻ <select> tìm kiếm
            ViewBag.SubjectList = new SelectList(_context.Subjects, "SubjectId", "SubjectName", subjectId);

            var query = _context.Questions.Include(q => q.Subject).AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(q => q.Content.Contains(keyword));
            }
            if (subjectId.HasValue)
            {
                query = query.Where(q => q.SubjectId == subjectId);
            }

            return View(await query.ToListAsync());
        }
    }
}