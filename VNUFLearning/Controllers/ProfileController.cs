using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VNUFLearning.Data;
using VNUFLearning.Models.ViewModels;
using VNUFLearning.Services;
using VNUFLearning.Services.Storage;

namespace VNUFLearning.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private static readonly string[] AllowedAvatarExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const long MaxAvatarSizeBytes = 5 * 1024 * 1024;

        private readonly VnufLearningContext _context;
        private readonly IMinioService _minioService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            VnufLearningContext context,
            IMinioService minioService,
            ILogger<ProfileController> logger)
        {
            _context = context;
            _minioService = minioService;
            _logger = logger;
        }

        [HttpGet("/Profile")]
        [HttpGet("/Student/Profile")]
        [HttpGet("/Teacher/Profile")]
        [HttpGet("/Admin/Profile")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == userId.Value);

                if (user == null)
                {
                    return NotFound("Không tìm thấy thông tin người dùng.");
                }

                var model = new UserProfileViewModel
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = user.Role.RoleName,
                    AvatarUrl = user.AvatarUrl,
                    JoinDate = user.CreatedAt,
                    Code = user.StudentCode,
                    Phone = user.Phone,
                    Bio = string.IsNullOrWhiteSpace(user.Bio) ? BuildBio(user.ClassName, user.DepartmentName) : user.Bio,
                    DepartmentName = user.DepartmentName,
                    RecentActivities = await GetRecentActivitiesAsync(user.UserId)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải trang hồ sơ cá nhân.");
                TempData["ErrorMessage"] = "Không thể tải hồ sơ cá nhân. Vui lòng thử lại sau.";
                return RedirectToDashboard();
            }
        }

        [HttpPost("/Profile/UpdateAvatar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAvatar(IFormFile avatarFile)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "Phiên đăng nhập không hợp lệ." });
                }

                if (avatarFile == null || avatarFile.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Vui lòng chọn ảnh đại diện." });
                }

                var uploadResult = await _minioService.UploadAsync(
                    avatarFile,
                    "avatars",
                    AllowedAvatarExtensions,
                    MaxAvatarSizeBytes);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });
                }

                user.AvatarUrl = uploadResult.Url;
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    avatarUrl = uploadResult.Url,
                    message = "Cập nhật ảnh đại diện thành công."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật ảnh đại diện.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdValue = User.FindFirstValue("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdValue, out var userId) ? userId : null;
        }

        private async Task<List<UserProfileActivityViewModel>> GetRecentActivitiesAsync(int userId)
        {
            // Lấy riêng từng nguồn để tránh làm phức tạp truy vấn EF, sau đó ghép timeline trên memory.
            var examActivities = await _context.ExamResults
                .AsNoTracking()
                .Include(r => r.Subject)
                .Include(r => r.Exam)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.FinishedAt ?? r.StartedAt)
                .Take(10)
                .Select(r => new UserProfileActivityViewModel
                {
                    Type = "Bài kiểm tra",
                    Title = r.Exam != null ? r.Exam.Title : $"Bài kiểm tra {r.Subject.SubjectName}",
                    Description = r.Score.HasValue
                        ? $"Điểm: {r.Score:0.##} - Trạng thái: {r.Status ?? "Hoàn thành"}"
                        : $"Trạng thái: {r.Status ?? "Đã tham gia"}",
                    CreatedAt = r.FinishedAt ?? r.StartedAt,
                    IconCssClass = "fa-solid fa-clipboard-check",
                    BadgeCssClass = "bg-success"
                })
                .ToListAsync();

            var blogActivities = await _context.BlogPosts
                .AsNoTracking()
                .Where(p => p.AuthorId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .Select(p => new UserProfileActivityViewModel
                {
                    Type = "Blog",
                    Title = p.Title,
                    Description = p.IsPublished ? "Đã đăng bài viết trên diễn đàn." : "Bài viết đang chờ hiển thị.",
                    CreatedAt = p.CreatedAt,
                    IconCssClass = "fa-regular fa-pen-to-square",
                    BadgeCssClass = "bg-primary"
                })
                .ToListAsync();

            return examActivities
                .Concat(blogActivities)
                .OrderByDescending(a => a.CreatedAt ?? DateTime.MinValue)
                .Take(12)
                .ToList();
        }

        private static string BuildBio(string? className, string? departmentName)
        {
            var parts = new[] { className, departmentName }
                .Where(value => !string.IsNullOrWhiteSpace(value));

            var bio = string.Join(" - ", parts);
            return string.IsNullOrWhiteSpace(bio)
                ? "Chưa có giới thiệu ngắn."
                : bio;
        }

        private IActionResult RedirectToDashboard()
        {
            var role = User.FindFirstValue(ClaimTypes.Role);

            return role switch
            {
                "Admin" => Redirect("/Admin/Dashboard"),
                "GiangVien" => Redirect("/Teacher/Dashboard"),
                "SinhVien" => Redirect("/Student/Dashboard"),
                _ => RedirectToAction("Login", "Account")
            };
        }
    }
}
