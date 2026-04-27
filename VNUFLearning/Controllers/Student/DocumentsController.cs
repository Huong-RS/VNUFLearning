using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;

namespace VNUFLearning.Controllers.Student
{
    [Authorize(Roles = "SinhVien")]
    [Route("Student/[controller]/[action]")]
    public class DocumentsController : Controller
    {
        private readonly VnufLearningContext _context;
        private readonly IWebHostEnvironment _env;

        public DocumentsController(VnufLearningContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet]
        [Route("~/Student/Documents")]
        public async Task<IActionResult> Index(string? keyword, int? subjectId)
        {
            var query = _context.Documents
                .Include(d => d.Subject)
                .Include(d => d.UploadedByNavigation)
                .Where(d => d.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(d => d.Title.Contains(keyword));
            }

            if (subjectId.HasValue && subjectId.Value > 0)
            {
                query = query.Where(d => d.SubjectId == subjectId.Value);
            }

            var documents = await query
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            ViewBag.Subjects = await _context.Subjects
                .Where(s => s.IsActive)
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            ViewBag.Keyword = keyword ?? "";
            ViewBag.SubjectId = subjectId ?? 0;

            return View("~/Views/Student/Documents/Index.cshtml", documents);
        }

        [HttpGet]
        [Route("~/Student/Documents/ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            var doc = await _context.Documents
                .FirstOrDefaultAsync(d => d.DocumentId == id && d.IsActive);

            if (doc == null) return NotFound();

            return Redirect(doc.FilePath);
        }

        [HttpGet]
        [Route("~/Student/Documents/Download/{id}")]
        public async Task<IActionResult> Download(int id)
        {
            var doc = await _context.Documents
                .FirstOrDefaultAsync(d => d.DocumentId == id && d.IsActive);

            if (doc == null) return NotFound();

            var path = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            doc.DownloadCount++;
            await _context.SaveChangesAsync();

            var bytes = await System.IO.File.ReadAllBytesAsync(path);
            return File(bytes, "application/octet-stream", doc.FileName ?? doc.Title);
        }
    }
}