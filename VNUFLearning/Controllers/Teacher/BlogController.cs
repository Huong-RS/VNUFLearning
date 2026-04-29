using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]
    [Route("Teacher/[controller]/[action]")]
    public class BlogController : Controller
    {
        private readonly VnufLearningContext _context;

        public BlogController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Teacher/Blog")]
        public async Task<IActionResult> Index(string? keyword, string tab = "feed")
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

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

            var pendingPosts = await query
                .Where(x => !x.IsPublished)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            ViewBag.Keyword = keyword ?? "";
            ViewBag.Tab = tab;
            ViewBag.PendingCount = pendingPosts.Count;
            ViewBag.CurrentTeacherId = teacherId.Value;
            ViewBag.PendingPosts = pendingPosts;

            return View("~/Views/Teacher/Blog/Index.cshtml", feedPosts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/Create")]
        public async Task<IActionResult> Create(BlogPost model, IFormFile? imageFile, IFormFile? attachmentFile)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Content))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ tiêu đề và nội dung bài viết.";
                return Redirect("/Teacher/Blog");
            }

            model.AuthorId = teacherId.Value;
            model.CreatedAt = DateTime.Now;
            model.ViewCount = 0;
            model.LikeCount = 0;
            model.IsPublished = true;

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
                    return Redirect("/Teacher/Blog");
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
                    return Redirect("/Teacher/Blog");
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

            TempData["Success"] = "Đăng bài thành công.";
            return Redirect("/Teacher/Blog");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/ToggleLikeAjax/{id}")]
        public async Task<IActionResult> ToggleLikeAjax(int id)
        {
            var userId = GetTeacherId();
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

            return Json(new
            {
                success = true,
                liked,
                count
            });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/CommentAjax/{id}")]
        public async Task<IActionResult> CommentAjax(int id, string? content, IFormFile? imageFile, IFormFile? attachmentFile)
        {
            var userId = GetTeacherId();
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
        [Route("~/Teacher/Blog/ReplyAjax/{commentId}")]
        public async Task<IActionResult> ReplyAjax(int commentId, string? content, IFormFile? imageFile, IFormFile? attachmentFile)
        {
            var userId = GetTeacherId();
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
        [Route("~/Teacher/Blog/Download/{id}")]
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/Approve/{id}")]
        public async Task<IActionResult> Approve(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return NotFound();

            post.IsPublished = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã phê duyệt bài viết.";
            return Redirect("/Teacher/Blog?tab=pending");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/Reject/{id}")]
        public async Task<IActionResult> Reject(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return NotFound();

            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã từ chối và xóa bài viết.";
            return Redirect("/Teacher/Blog?tab=pending");
        }
        [HttpGet]
        [Route("~/Teacher/Blog/DownloadCommentFile/{id}")]
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/DeleteComment/{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Unauthorized();

            var comment = await _context.Comments
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.CommentId == id);

            if (comment == null) return NotFound();

            // Giảng viên được xóa mọi comment trong blog
            _context.Comments.RemoveRange(comment.Replies);
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var post = await _context.BlogPosts
                .FirstOrDefaultAsync(x => x.PostId == id);

            if (post == null) return NotFound();

            // Lấy toàn bộ comment của bài viết
            var comments = await _context.Comments
                .Where(c => c.PostId == id)
                .ToListAsync();

            // Xóa reply trước
            var replies = comments
                .Where(c => c.ParentCommentId != null)
                .ToList();

            if (replies.Any())
                _context.Comments.RemoveRange(replies);

            // Xóa comment gốc sau
            var rootComments = comments
                .Where(c => c.ParentCommentId == null)
                .ToList();

            if (rootComments.Any())
                _context.Comments.RemoveRange(rootComments);

            // Xóa lượt tim của bài viết
            var likes = await _context.BlogLikes
                .Where(x => x.PostId == id)
                .ToListAsync();

            if (likes.Any())
                _context.BlogLikes.RemoveRange(likes);

            // Xóa bài viết
            _context.BlogPosts.Remove(post);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa bài viết.";
            return Redirect("/Teacher/Blog");
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
        private int? GetTeacherId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : null;
        }

        private string GetInitial(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "U";

            return fullName.Trim().Substring(0, 1).ToUpper();
        }
    }
}