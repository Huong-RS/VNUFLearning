using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;
using VNUFLearning.Services.Storage;
namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]
    [Route("Teacher/[controller]/[action]")]
    public class BlogController : Controller
    {
        private readonly VnufLearningContext _context;
        private readonly IMinioService _minioService;

        public BlogController(VnufLearningContext context, IMinioService minioService)
        {
            _context = context;
            _minioService = minioService;
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

            var allowedImages = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4" };
            var allowedDocs = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".zip", ".rar" };

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploaded = await _minioService.UploadAsync(
                    imageFile,
                    "blog/images",
                    allowedImages);

                model.ImageUrl = uploaded.Url;
                model.ImageObjectName = uploaded.ObjectName;
            }

            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                var uploaded = await _minioService.UploadAsync(
                    attachmentFile,
                    "blog/attachments",
                    allowedDocs);

                model.AttachmentUrl = uploaded.Url;
                model.AttachmentObjectName = uploaded.ObjectName;
                model.AttachmentName = uploaded.OriginalFileName;
                model.AttachmentType = Path.GetExtension(uploaded.OriginalFileName).Replace(".", "").ToUpper();
                model.AttachmentSize = uploaded.Size;
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
                commentId = reply.CommentId,
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

        if (post == null || string.IsNullOrWhiteSpace(post.AttachmentObjectName))
            return NotFound();

        var fileUrl = post.AttachmentUrl;
        return Redirect(fileUrl);
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

            var postHistories = await _context.BlogPostEditHistories
    .Where(x => x.PostId == id)
    .ToListAsync();

            if (postHistories.Any())
                _context.BlogPostEditHistories.RemoveRange(postHistories);

            await _minioService.DeleteAsync(post.ImageObjectName);
            await _minioService.DeleteAsync(post.AttachmentObjectName);

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

        if (comment == null || string.IsNullOrWhiteSpace(comment.AttachmentObjectName))
            return NotFound();

        return Redirect(comment.AttachmentUrl);
    }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/DeleteComment/{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Unauthorized();

            var rootComment = await _context.Comments
                .Include(x => x.User)
                    .ThenInclude(u => u.Role)
                .FirstOrDefaultAsync(x => x.CommentId == id);

            if (rootComment == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy bình luận."
                });
            }

            var isOwnComment = rootComment.UserId == teacherId.Value;
            var isStudentComment = rootComment.User.Role.RoleName == "SinhVien";

            if (!isOwnComment && !isStudentComment)
            {
                return Json(new
                {
                    success = false,
                    message = "Bạn không được xóa bình luận của giảng viên khác."
                });
            }

            var commentsToDelete = await GetCommentTreeAsync(id);
            var commentIds = commentsToDelete.Select(x => x.CommentId).ToList();

            var histories = await _context.CommentEditHistories
                .Where(x => commentIds.Contains(x.CommentId))
                .ToListAsync();

            if (histories.Any())
                _context.CommentEditHistories.RemoveRange(histories);

            foreach (var comment in commentsToDelete)
            {
                await _minioService.DeleteAsync(comment.ImageObjectName);
                await _minioService.DeleteAsync(comment.AttachmentObjectName);
            }

            _context.Comments.RemoveRange(commentsToDelete);

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                deletedIds = commentIds
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var post = await _context.BlogPosts
       .Include(x => x.Author)
       .ThenInclude(x => x.Role)
       .FirstOrDefaultAsync(x => x.PostId == id);

            if (post == null) return NotFound();

            var isOwnPost = post.AuthorId == teacherId.Value;
            var isStudentPost = post.Author.Role.RoleName == "SinhVien";

            if (!isOwnPost && !isStudentPost)
            {
                return Forbid();
            }

            var comments = await _context.Comments
                .Where(c => c.PostId == id)
                .ToListAsync();

            var commentIds = comments.Select(x => x.CommentId).ToList();

            var commentHistories = await _context.CommentEditHistories
                .Where(x => commentIds.Contains(x.CommentId))
                .ToListAsync();

            if (commentHistories.Any())
                _context.CommentEditHistories.RemoveRange(commentHistories);

            foreach (var c in comments)
            {
                await _minioService.DeleteAsync(c.ImageObjectName);
                await _minioService.DeleteAsync(c.AttachmentObjectName);
            }

            if (comments.Any())
                _context.Comments.RemoveRange(comments);

            var likes = await _context.BlogLikes
                .Where(x => x.PostId == id)
                .ToListAsync();

            if (likes.Any())
                _context.BlogLikes.RemoveRange(likes);

            var postHistories = await _context.BlogPostEditHistories
                .Where(x => x.PostId == id)
                .ToListAsync();

            if (postHistories.Any())
                _context.BlogPostEditHistories.RemoveRange(postHistories);

            await _minioService.DeleteAsync(post.ImageObjectName);
            await _minioService.DeleteAsync(post.AttachmentObjectName);

            _context.BlogPosts.Remove(post);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa bài viết.";
            return Redirect("/Teacher/Blog");
        }
        private async Task SaveCommentFiles(Comment comment, IFormFile? imageFile, IFormFile? attachmentFile)
    {
        var allowedImages = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4" };
        var allowedDocs = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".zip", ".rar" };

        if (imageFile != null && imageFile.Length > 0)
        {
            var uploaded = await _minioService.UploadAsync(
                imageFile,
                "blog-comments/images",
                allowedImages);

            comment.ImageUrl = uploaded.Url;
            comment.ImageObjectName = uploaded.ObjectName;
        }

        if (attachmentFile != null && attachmentFile.Length > 0)
        {
            var uploaded = await _minioService.UploadAsync(
                attachmentFile,
                "blog-comments/attachments",
                allowedDocs);

            comment.AttachmentUrl = uploaded.Url;
            comment.AttachmentObjectName = uploaded.ObjectName;
            comment.AttachmentName = uploaded.OriginalFileName;
            comment.AttachmentType = Path.GetExtension(uploaded.OriginalFileName).Replace(".", "").ToUpper();
            comment.AttachmentSize = uploaded.Size;
        }
    }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/EditPost/{id}")]
        public async Task<IActionResult> EditPost(
            int id,
            string title,
            string content,
               IFormFile? imageFile,
                IFormFile? attachmentFile,
                bool removeImage = false,
                bool removeAttachment = false)
        {
            var userId = GetTeacherId();
            if (userId == null) return Unauthorized();

            var post = await _context.BlogPosts
                .FirstOrDefaultAsync(x => x.PostId == id && x.AuthorId == userId.Value);

            if (post == null)
                return Json(new { success = false, message = "Bạn chỉ được sửa bài viết của chính mình." });

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "Tiêu đề và nội dung không được để trống." });

            _context.BlogPostEditHistories.Add(new BlogPostEditHistory
            {
                PostId = post.PostId,
                OldTitle = post.Title,
                OldContent = post.Content,
                OldImageUrl = post.ImageUrl,
                OldAttachmentUrl = post.AttachmentUrl,
                EditedByUserId = userId.Value,
                EditedAt = DateTime.Now
            });

            var allowedImages = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4" };
            var allowedDocs = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".zip", ".rar" };

            post.Title = title.Trim();
            post.Content = content.Trim();

            if (removeImage)
            {
                await _minioService.DeleteAsync(post.ImageObjectName);
                post.ImageUrl = null;
                post.ImageObjectName = null;
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                var oldImageObjectName = post.ImageObjectName;

                var uploaded = await _minioService.UploadAsync(
                    imageFile,
                    "blog/images",
                    allowedImages);

                post.ImageUrl = uploaded.Url;
                post.ImageObjectName = uploaded.ObjectName;

                await _minioService.DeleteAsync(oldImageObjectName);
            }

            if (removeAttachment)
            {
                await _minioService.DeleteAsync(post.AttachmentObjectName);
                post.AttachmentUrl = null;
                post.AttachmentObjectName = null;
                post.AttachmentName = null;
                post.AttachmentType = null;
                post.AttachmentSize = null;
            }

            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                var oldAttachmentObjectName = post.AttachmentObjectName;

                var uploaded = await _minioService.UploadAsync(
                    attachmentFile,
                    "blog/attachments",
                    allowedDocs);

                post.AttachmentUrl = uploaded.Url;
                post.AttachmentObjectName = uploaded.ObjectName;
                post.AttachmentName = uploaded.OriginalFileName;
                post.AttachmentType = Path.GetExtension(uploaded.OriginalFileName).Replace(".", "").ToUpper();
                post.AttachmentSize = uploaded.Size;

                await _minioService.DeleteAsync(oldAttachmentObjectName);
            }

            post.IsEdited = true;
            post.LastEditedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                title = post.Title,
                content = post.Content,
                imageUrl = post.ImageUrl,
                attachmentUrl = post.AttachmentUrl,
                attachmentName = post.AttachmentName,
                attachmentType = post.AttachmentType,
                attachmentSize = post.AttachmentSize.HasValue
                    ? (post.AttachmentSize.Value / 1024.0 / 1024.0).ToString("0.##") + " MB"
                    : ""
            });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Blog/EditComment/{id}")]
        public async Task<IActionResult> EditComment(int id, string content)
        {
            var userId = GetTeacherId();
            if (userId == null) return Unauthorized();

            var comment = await _context.Comments
                .FirstOrDefaultAsync(x => x.CommentId == id && x.UserId == userId.Value);

            if (comment == null)
                return Json(new { success = false, message = "Bạn chỉ được sửa bình luận của chính mình." });

            if (string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "Nội dung bình luận không được để trống." });

            _context.CommentEditHistories.Add(new CommentEditHistory
            {
                CommentId = comment.CommentId,
                OldContent = comment.Content,
                OldImageUrl = comment.ImageUrl,
                OldAttachmentUrl = comment.AttachmentUrl,
                EditedByUserId = userId.Value,
                EditedAt = DateTime.Now
            });

            comment.Content = content.Trim();
            comment.IsEdited = true;
            comment.LastEditedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                content = comment.Content
            });
        }

        [HttpGet]
        [Route("~/Teacher/Blog/PostHistory/{id}")]
        public async Task<IActionResult> PostHistory(int id)
        {
            var histories = await _context.BlogPostEditHistories
                .Include(x => x.EditedByUser)
                .Where(x => x.PostId == id)
                .OrderByDescending(x => x.EditedAt)
                .Select(x => new
                {
                    editedBy = x.EditedByUser.FullName,
                    editedAt = x.EditedAt.ToString("HH:mm dd/MM/yyyy"),
                    oldTitle = x.OldTitle,
                    oldContent = x.OldContent
                })
                .ToListAsync();

            return Json(histories);
        }

        [HttpGet]
        [Route("~/Teacher/Blog/CommentHistory/{id}")]
        public async Task<IActionResult> CommentHistory(int id)
        {
            var histories = await _context.CommentEditHistories
                .Include(x => x.EditedByUser)
                .Where(x => x.CommentId == id)
                .OrderByDescending(x => x.EditedAt)
                .Select(x => new
                {
                    editedBy = x.EditedByUser.FullName,
                    editedAt = x.EditedAt.ToString("HH:mm dd/MM/yyyy"),
                    oldContent = x.OldContent
                })
                .ToListAsync();

            return Json(histories);
        }
        private async Task<List<Comment>> GetCommentTreeAsync(int rootCommentId)
        {
            var root = await _context.Comments
                .FirstOrDefaultAsync(c => c.CommentId == rootCommentId);

            if (root == null)
                return new List<Comment>();

            var allPostComments = await _context.Comments
                .Where(c => c.PostId == root.PostId)
                .ToListAsync();

            var result = new List<Comment>();

            void Collect(Comment comment)
            {
                result.Add(comment);

                var children = allPostComments
                    .Where(c => c.ParentCommentId == comment.CommentId)
                    .ToList();

                foreach (var child in children)
                {
                    Collect(child);
                }
            }

            Collect(root);

            return result;
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