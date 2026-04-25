using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]
    [Route("Teacher/[controller]/[action]")]
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
        [Route("~/Teacher/Documents")]
        public async Task<IActionResult> Index(int? subjectId, string? keyword)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var assignedSubjectIds = await _context.SubjectAssignments
                .Where(x => x.TeacherId == userId.Value)
                .Select(x => x.SubjectId)
                .ToListAsync();

            var subjects = await _context.Subjects
                .Where(s => assignedSubjectIds.Contains(s.SubjectId) && s.IsActive)
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            var query = _context.Documents
                .Include(d => d.Subject)
                .Include(d => d.UploadedByNavigation)
                .Where(d => d.IsActive && assignedSubjectIds.Contains(d.SubjectId))
                .AsQueryable();

            if (subjectId.HasValue && subjectId.Value > 0)
            {
                query = query.Where(d => d.SubjectId == subjectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(d => d.Title.Contains(keyword));
            }

            var documents = await query
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            ViewBag.Subjects = subjects;
            ViewBag.SubjectId = subjectId ?? 0;
            ViewBag.Keyword = keyword ?? "";

            ViewBag.TotalDocuments = documents.Count;
            ViewBag.TotalStorage = documents.Sum(d => d.FileSize ?? 0);
            ViewBag.MyUploads = documents.Count(d => d.UploadedBy == userId.Value);

            return View("~/Views/Teacher/Documents/Index.cshtml", documents);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Documents/Upload")]
        public async Task<IActionResult> Upload(IFormFile file, int subjectId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["Error"] = "Phiên đăng nhập không hợp lệ.";
                return RedirectToAction("Login", "Account");
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file cần tải lên.";
                return Redirect("/Teacher/Documents");
            }

            var canUpload = await _context.SubjectAssignments
                .AnyAsync(x => x.TeacherId == userId.Value && x.SubjectId == subjectId);

            if (!canUpload)
            {
                TempData["Error"] = "Bạn không được phân công môn học này nên không thể tải tài liệu.";
                return Redirect("/Teacher/Documents");
            }

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".zip", ".rar", ".xlsx", ".xls" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Chỉ cho phép tải lên PDF, Word, PowerPoint, Excel hoặc file nén.";
                return Redirect("/Teacher/Documents");
            }

            if (file.Length > 50 * 1024 * 1024)
            {
                TempData["Error"] = "File không được vượt quá 50MB.";
                return Redirect("/Teacher/Documents");
            }

            var folder = Path.Combine(_env.WebRootPath, "uploads", "documents");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var savedFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(folder, savedFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var document = new Document
            {
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                FileName = file.FileName,
                FilePath = "/uploads/documents/" + savedFileName,
                FileType = extension.Replace(".", "").ToUpper(),
                FileSize = file.Length,
                SubjectId = subjectId,
                UploadedBy = userId.Value,
                CreatedAt = DateTime.Now,
                IsActive = true,
                DownloadCount = 0
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tải tài liệu lên thành công.";
            return Redirect("/Teacher/Documents");
        }

        [HttpGet]
        [Route("~/Teacher/Documents/Download/{id}")]
        public async Task<IActionResult> Download(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var doc = await _context.Documents
                .Include(d => d.Subject)
                .FirstOrDefaultAsync(d => d.DocumentId == id && d.IsActive);

            if (doc == null) return NotFound();

            var canAccess = await _context.SubjectAssignments
                .AnyAsync(x => x.TeacherId == userId.Value && x.SubjectId == doc.SubjectId);

            if (!canAccess) return Forbid();

            var path = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(path))
            {
                TempData["Error"] = "Không tìm thấy file trên server.";
                return Redirect("/Teacher/Documents");
            }

            doc.DownloadCount++;
            await _context.SaveChangesAsync();

            var bytes = await System.IO.File.ReadAllBytesAsync(path);
            return File(bytes, "application/octet-stream", doc.FileName ?? doc.Title);
        }

        [HttpGet]
        [Route("~/Teacher/Documents/ViewFile/{id}")]
        public async Task<IActionResult> ViewFile(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var doc = await _context.Documents
                .FirstOrDefaultAsync(d => d.DocumentId == id && d.IsActive);

            if (doc == null) return NotFound();

            var canAccess = await _context.SubjectAssignments
                .AnyAsync(x => x.TeacherId == userId.Value && x.SubjectId == doc.SubjectId);

            if (!canAccess) return Forbid();

            return Redirect(doc.FilePath);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Documents/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var doc = await _context.Documents
                .FirstOrDefaultAsync(d => d.DocumentId == id && d.IsActive);

            if (doc == null) return NotFound();

            if (doc.UploadedBy != userId.Value)
            {
                TempData["Error"] = "Bạn chỉ được xóa tài liệu do chính mình tải lên.";
                return RedirectToAction(nameof(Index));
            }

            doc.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa tài liệu.";
            return Redirect("/Teacher/Documents");
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            return null;
        }
    }
}