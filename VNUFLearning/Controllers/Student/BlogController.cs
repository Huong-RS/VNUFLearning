using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Student
{
    [Authorize(Roles = "SinhVien")]
    [Route("Student/[controller]/[action]")]
    public class BlogController : Controller
    {
        private readonly VnufLearningContext _context;

        public BlogController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Student/Blog")]
        public async Task<IActionResult> Index(string? keyword, string tab = "feed")
        {
            var studentId = GetStudentId();
            if (studentId == null) return Redirect("/Account/Login");

            var query = _context.BlogPosts
                .Include(x => x.Author)
                .Include(x => x.BlogLikes)
                .Include(x => x.Comments)
                    .ThenInclude(c => c.User)
                .Include(x => x.Comments)
                    .ThenInclude(c => c.Replies)
                        .ThenInclude(r => r.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(x =>
                    x.Title.Contains(keyword) ||
                    x.Content.Contains(keyword) ||
                    x.Author.FullName.Contains(keyword));
            }

            var feedPosts = await query
                .Where(x => x.IsPublished)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var myPendingPosts = await query
                .Where(x => x.AuthorId == studentId.Value && !x.IsPublished)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            ViewBag.Keyword = keyword ?? "";
            ViewBag.Tab = tab;
            ViewBag.CurrentStudentId = studentId.Value;
            ViewBag.MyPendingPosts = myPendingPosts;
            ViewBag.PendingCount = myPendingPosts.Count;

            return View("~/Views/Student/Blog/Index.cshtml", feedPosts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Student/Blog/Create")]
        public async Task<IActionResult> Create(BlogPost model, IFormFile? imageFile, IFormFile? attachmentFile)
        {
            var studentId = GetStudentId();
            if (studentId == null) return Redirect("/Account/Login");

            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Content))
            {
                TempData["Error"] = "Vui lòng nhập tiêu đề và nội dung bài viết.";
                return Redirect("/Student/Blog");
            }

            model.AuthorId = studentId.Value;
            model.CreatedAt = DateTime.Now;
            model.ViewCount = 0;
            model.LikeCount = 0;

            // Sinh viên đăng bài cần giảng viên duyệt
            model.IsPublished = false;

            var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "blogs");
            if (!Directory.Exists(uploadRoot))
                Directory.CreateDirectory(uploadRoot);

            if (imageFile != null && imageFile.Length > 0)
            {
                var ext = Path.GetExtension(imageFile.FileName).ToLower();
                var allowedImages = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4" };

                if (!allowedImages.Contains(ext))
                {
                    TempData["Error"] = "Ảnh/Video chỉ hỗ trợ: jpg, jpeg, png, gif, webp, mp4.";
                    return Redirect("/Student/Blog");
                }

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadRoot, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);

                model.ImageUrl = $"/uploads/blogs/{fileName}";
            }

            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                var ext = Path.GetExtension(attachmentFile.FileName).ToLower();
                var allowedDocs = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".zip", ".rar" };

                if (!allowedDocs.Contains(ext))
                {
                    TempData["Error"] = "File đính kèm chỉ hỗ trợ: pdf, doc, docx, ppt, pptx, xls, xlsx, zip, rar.";
                    return Redirect("/Student/Blog");
                }

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadRoot, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await attachmentFile.CopyToAsync(stream);

                model.AttachmentUrl = $"/uploads/blogs/{fileName}";
                model.AttachmentName = attachmentFile.FileName;
                model.AttachmentType = ext.Replace(".", "").ToUpper();
                model.AttachmentSize = attachmentFile.Length;
            }

            _context.BlogPosts.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Bài viết đã được gửi. Vui lòng chờ giảng viên phê duyệt.";
            return Redirect("/Student/Blog?tab=pending");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Student/Blog/ToggleLikeAjax/{id}")]
        public async Task<IActionResult> ToggleLikeAjax(int id)
        {
            var userId = GetStudentId();
            if (userId == null) return Unauthorized();

            var like = await _context.BlogLikes
                .FirstOrDefaultAsync(x => x.PostId == id && x.UserId == userId.Value);

            var liked = false;

            if (like == null)
            {
                _context.BlogLikes.Add(new BlogLike
                {
                    PostId = id,
                    UserId = userId.Value,
                    CreatedAt = DateTime.Now
                });

                liked = true;
            }
            else
            {
                _context.BlogLikes.Remove(like);
            }

            await _context.SaveChangesAsync();

            var count = await _context.BlogLikes.CountAsync(x => x.PostId == id);

            return Json(new { success = true, liked, count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Student/Blog/CommentAjax/{id}")]
        public async Task<IActionResult> CommentAjax(int id, string? content, IFormFile? imageFile, IFormFile? attachmentFile)
        {
            var userId = GetStudentId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(content) && imageFile == null && attachmentFile == null)
                return Json(new { success = false, message = "Bình luận không được để trống." });

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return Unauthorized();

            var comment = new Comment
            {
                PostId = id,
                UserId = userId.Value,
                Content = content?.Trim() ?? "",
                CreatedAt = DateTime.Now
            };

            await SaveCommentFiles(comment, imageFile, attachmentFile);

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                commentId = comment.CommentId,
                userName = user.FullName,
                avatar = user.FullName.Substring(0, 1).ToUpper(),
                content = comment.Content,
                imageUrl = comment.ImageUrl,
                attachmentUrl = comment.AttachmentUrl,
                attachmentName = comment.AttachmentName,
                attachmentType = comment.AttachmentType,
                attachmentSize = comment.AttachmentSize.HasValue
                    ? (comment.AttachmentSize.Value / 1024.0 / 1024.0).ToString("0.##") + " MB"
                    : "",
                createdAt = comment.CreatedAt?.ToString("HH:mm dd/MM")
            });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Student/Blog/DeleteComment/{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var userId = GetStudentId();
            if (userId == null) return Unauthorized();

            var comment = await _context.Comments
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.CommentId == id && c.UserId == userId.Value);

            if (comment == null) return NotFound();

            _context.Comments.RemoveRange(comment.Replies);
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Student/Blog/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetStudentId();
            if (userId == null) return Redirect("/Account/Login");

            var post = await _context.BlogPosts
                .FirstOrDefaultAsync(x => x.PostId == id && x.AuthorId == userId.Value);

            if (post == null) return NotFound();

            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa bài viết.";
            return Redirect("/Student/Blog");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Student/Blog/ReplyAjax/{commentId}")]
        public async Task<IActionResult> ReplyAjax(int commentId, string? content, IFormFile? imageFile, IFormFile? attachmentFile)
        {
            var userId = GetStudentId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(content) && imageFile == null && attachmentFile == null)
                return Json(new { success = false, message = "Phản hồi không được để trống." });

            var parent = await _context.Comments.FindAsync(commentId);
            if (parent == null) return NotFound();

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null) return Unauthorized();

            var reply = new Comment
            {
                PostId = parent.PostId,
                ParentCommentId = commentId,
                UserId = userId.Value,
                Content = content?.Trim() ?? "",
                CreatedAt = DateTime.Now
            };

            await SaveCommentFiles(reply, imageFile, attachmentFile);

            _context.Comments.Add(reply);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                parentId = commentId,
                userName = user.FullName,
                avatar = user.FullName.Substring(0, 1).ToUpper(),
                content = reply.Content,
                imageUrl = reply.ImageUrl,
                attachmentUrl = reply.AttachmentUrl,
                attachmentName = reply.AttachmentName,
                attachmentType = reply.AttachmentType,
                attachmentSize = reply.AttachmentSize.HasValue
                    ? (reply.AttachmentSize.Value / 1024.0 / 1024.0).ToString("0.##") + " MB"
                    : ""
            });
        }

          

        [HttpGet]
        [Route("~/Student/Blog/Download/{id}")]
        public async Task<IActionResult> Download(int id)
        {
            var post = await _context.BlogPosts.FirstOrDefaultAsync(x => x.PostId == id);

            if (post == null || string.IsNullOrWhiteSpace(post.AttachmentUrl))
                return NotFound();

            var filePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                post.AttachmentUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
            );

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileName = post.AttachmentName ?? Path.GetFileName(filePath);
            return PhysicalFile(filePath, "application/octet-stream", fileName);
        }
        [HttpGet]
        [Route("~/Student/Blog/DownloadCommentFile/{id}")]
        public async Task<IActionResult> DownloadCommentFile(int id)
        {
            var comment = await _context.Comments.FirstOrDefaultAsync(x => x.CommentId == id);

            if (comment == null || string.IsNullOrWhiteSpace(comment.AttachmentUrl))
                return NotFound();

            var filePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                comment.AttachmentUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString())
            );

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            return PhysicalFile(filePath, "application/octet-stream", comment.AttachmentName ?? Path.GetFileName(filePath));
        }
        private async Task SaveCommentFiles(Comment comment, IFormFile? imageFile, IFormFile? attachmentFile)
        {
            var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "blog-comments");

            if (!Directory.Exists(uploadRoot))
                Directory.CreateDirectory(uploadRoot);

            if (imageFile != null && imageFile.Length > 0)
            {
                var ext = Path.GetExtension(imageFile.FileName).ToLower();
                var allowedImages = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4" };

                if (!allowedImages.Contains(ext))
                    throw new Exception("Ảnh/Video bình luận chỉ hỗ trợ jpg, png, gif, webp, mp4.");

                var fileName = $"{Guid.NewGuid()}{ext}";
                var path = Path.Combine(uploadRoot, fileName);

                using var stream = new FileStream(path, FileMode.Create);
                await imageFile.CopyToAsync(stream);

                comment.ImageUrl = $"/uploads/blog-comments/{fileName}";
            }

            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                var ext = Path.GetExtension(attachmentFile.FileName).ToLower();
                var allowedDocs = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".zip", ".rar" };

                if (!allowedDocs.Contains(ext))
                    throw new Exception("File bình luận chỉ hỗ trợ pdf, doc, docx, ppt, pptx, xls, xlsx, zip, rar.");

                var fileName = $"{Guid.NewGuid()}{ext}";
                var path = Path.Combine(uploadRoot, fileName);

                using var stream = new FileStream(path, FileMode.Create);
                await attachmentFile.CopyToAsync(stream);

                comment.AttachmentUrl = $"/uploads/blog-comments/{fileName}";
                comment.AttachmentName = attachmentFile.FileName;
                comment.AttachmentType = ext.Replace(".", "").ToUpper();
                comment.AttachmentSize = attachmentFile.Length;
            }
        }
        private int? GetStudentId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : null;
        }
    }
}