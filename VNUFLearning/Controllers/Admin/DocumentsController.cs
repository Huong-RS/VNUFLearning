using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
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
        [Route("~/Admin/Documents")]
        public async Task<IActionResult> Index(int? subjectId, int? teacherId, string? keyword)
        {
            var query = _context.Documents
                .Include(d => d.Subject)
                .Include(d => d.UploadedByNavigation)
                .Where(d => d.IsActive)
                .AsQueryable();

            if (subjectId.HasValue && subjectId.Value > 0)
                query = query.Where(d => d.SubjectId == subjectId.Value);

            if (teacherId.HasValue && teacherId.Value > 0)
                query = query.Where(d => d.UploadedBy == teacherId.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(d => d.Title.Contains(keyword.Trim()));

            var documents = await query
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            ViewBag.Subjects = await _context.Subjects
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            ViewBag.Teachers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "GiangVien")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.TotalDocuments = await _context.Documents.CountAsync(d => d.IsActive);
            ViewBag.TotalStorage = await _context.Documents.Where(d => d.IsActive).SumAsync(d => d.FileSize ?? 0);
            ViewBag.RecentUploads = await _context.Documents.CountAsync(d => d.IsActive && d.CreatedAt >= DateTime.Now.AddDays(-7));
            ViewBag.Reported = 0;

            ViewBag.Keyword = keyword ?? "";
            ViewBag.SubjectId = subjectId ?? 0;
            ViewBag.TeacherId = teacherId ?? 0;

            return View("~/Views/Admin/Documents/Index.cshtml", documents);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Documents/Upload")]
        public async Task<IActionResult> Upload(IFormFile file, int subjectId)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file.";
                return RedirectToAction(nameof(Index));
            }

            if (subjectId <= 0)
            {
                TempData["Error"] = "Chưa chọn môn học.";
                return RedirectToAction(nameof(Index));
            }

            // ✅ FIX NULL USER
            var userIdClaim = User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                TempData["Error"] = "Phiên đăng nhập lỗi.";
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdClaim);

            // ✅ CHECK SIZE (20MB)
            if (file.Length > 20 * 1024 * 1024)
            {
                TempData["Error"] = "File vượt quá 20MB.";
                return RedirectToAction(nameof(Index));
            }

            var allowed = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".zip", ".rar", ".xlsx", ".xls" };
            var ext = Path.GetExtension(file.FileName).ToLower();

            if (!allowed.Contains(ext))
            {
                TempData["Error"] = "Định dạng không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "documents");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var newFileName = Guid.NewGuid() + ext;
            var fullPath = Path.Combine(folder, newFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var doc = new Document
            {
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                FileName = file.FileName,
                FilePath = "/uploads/documents/" + newFileName,
                FileType = ext.Replace(".", "").ToUpper(),
                FileSize = file.Length,
                SubjectId = subjectId,
                UploadedBy = userId,
                CreatedAt = DateTime.Now,
                DownloadCount = 0,
                IsActive = true
            };

            _context.Documents.Add(doc);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Upload thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("~/Admin/Documents/Download/{id}")]
        public async Task<IActionResult> Download(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            var path = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(path))
            {
                TempData["Error"] = "Không tìm thấy file trên server.";
                return RedirectToAction(nameof(Index));
            }

            doc.DownloadCount++;
            await _context.SaveChangesAsync();

            var bytes = await System.IO.File.ReadAllBytesAsync(path);
            return File(bytes, "application/octet-stream", doc.FileName ?? doc.Title);
        }

        [HttpGet]
        [Route("~/Admin/Documents/ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            return Redirect(doc.FilePath);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Documents/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc != null)
            {
                doc.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa tài liệu khỏi danh sách.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}