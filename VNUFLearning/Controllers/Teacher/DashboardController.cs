using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models.ViewModels;

namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]
    [Route("Teacher/[controller]/[action]")]
    public class DashboardController : Controller
    {
        private readonly VnufLearningContext _context;

        public DashboardController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Teacher/Dashboard/Index")]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet]
        [Route("~/Teacher")]
        [Route("~/Teacher/Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var teacherId = GetTeacherId();
            if (teacherId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var teacher = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == teacherId.Value);

            if (teacher == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Lay danh sach mon hoc dang active ma giang vien duoc phan cong phu trach.
            var assignedSubjects = await _context.SubjectAssignments
                .AsNoTracking()
                .Include(sa => sa.Subject)
                .Where(sa => sa.TeacherId == teacherId.Value && sa.Subject.IsActive)
                .Select(sa => sa.Subject)
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            var subjectIds = assignedSubjects.Select(s => s.SubjectId).ToList();

            // Dem bai thi dang cho AI cham trong cac de thi thuoc giang vien hien tai.
            var pendingExamsToGrade = await _context.ExamResults
                .AsNoTracking()
                .Include(r => r.Exam)
                .Where(r => r.Exam != null &&
                            r.Exam.TeacherId == teacherId.Value &&
                            r.Status == "AI_DANG_CHAM")
                .CountAsync();

            // Blog cho duyet la cac bai chua publish trong luong moderation cua giang vien.
            var pendingBlogsToApprove = await _context.BlogPosts
                .AsNoTracking()
                .Where(p => !p.IsPublished)
                .CountAsync();

            // He thong chua co bang Enrollment/Class rieng, nen tam tinh sinh vien theo nguoi hoc
            // da co ket qua thi trong cac mon ma giang vien phu trach.
            var totalStudents = await _context.ExamResults
                .AsNoTracking()
                .Where(r => subjectIds.Contains(r.SubjectId))
                .Select(r => r.UserId)
                .Distinct()
                .CountAsync();

            var activeCourses = new List<CourseViewModel>();
            foreach (var subject in assignedSubjects)
            {
                activeCourses.Add(new CourseViewModel
                {
                    SubjectId = subject.SubjectId,
                    CourseCode = subject.SubjectCode ?? $"MH{subject.SubjectId}",
                    CourseName = subject.SubjectName,
                    StudentCount = await _context.ExamResults
                        .AsNoTracking()
                        .Where(r => r.SubjectId == subject.SubjectId)
                        .Select(r => r.UserId)
                        .Distinct()
                        .CountAsync(),
                    ExamCount = await _context.Exams
                        .AsNoTracking()
                        .CountAsync(e => e.TeacherId == teacherId.Value && e.SubjectId == subject.SubjectId),
                    DocumentCount = await _context.Documents
                        .AsNoTracking()
                        .CountAsync(d => d.SubjectId == subject.SubjectId && d.IsActive)
                });
            }

            var model = new TeacherDashboardViewModel
            {
                TeacherName = teacher.FullName,
                AvatarUrl = teacher.AvatarUrl,
                TotalCourses = activeCourses.Count,
                TotalStudents = totalStudents,
                PendingExamsToGrade = pendingExamsToGrade,
                PendingBlogsToApprove = pendingBlogsToApprove,
                TodoItems = BuildTodoItems(pendingExamsToGrade, pendingBlogsToApprove),
                ActiveCourses = activeCourses,
                RecentActivities = await GetRecentActivitiesAsync(teacherId.Value, subjectIds)
            };

            ViewData["Title"] = "Dashboard Gi\u1ea3ng vi\u00ean";
            return View("~/Views/Teacher/Dashboard/Dashboard.cshtml", model);
        }

        private int? GetTeacherId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var teacherId) ? teacherId : null;
        }

        private static List<TodoItemViewModel> BuildTodoItems(int pendingExamsToGrade, int pendingBlogsToApprove)
        {
            var items = new List<TodoItemViewModel>();

            if (pendingExamsToGrade > 0)
            {
                items.Add(new TodoItemViewModel
                {
                    Title = "B\u00e0i thi ch\u1edd AI ch\u1ea5m",
                    Description = "Ki\u1ec3m tra c\u00e1c b\u00e0i thi \u0111ang ch\u1edd AI x\u1eed l\u00fd ho\u1eb7c c\u1ea7n gi\u1ea3ng vi\u00ean duy\u1ec7t l\u1ea1i.",
                    Url = "/Teacher/Exams",
                    Count = pendingExamsToGrade,
                    IconCssClass = "fa-solid fa-clipboard-check"
                });
            }

            if (pendingBlogsToApprove > 0)
            {
                items.Add(new TodoItemViewModel
                {
                    Title = "Blog ch\u1edd duy\u1ec7t",
                    Description = "Duy\u1ec7t ho\u1eb7c t\u1eeb ch\u1ed1i c\u00e1c b\u00e0i vi\u1ebft ch\u01b0a \u0111\u01b0\u1ee3c xu\u1ea5t b\u1ea3n tr\u00ean di\u1ec5n \u0111\u00e0n.",
                    Url = "/Teacher/Blog?tab=pending",
                    Count = pendingBlogsToApprove,
                    IconCssClass = "fa-regular fa-newspaper"
                });
            }

            if (!items.Any())
            {
                items.Add(new TodoItemViewModel
                {
                    Title = "Kh\u00f4ng c\u00f3 c\u00f4ng vi\u1ec7c kh\u1ea9n",
                    Description = "C\u00e1c h\u00e0ng ch\u1edd x\u1eed l\u00fd hi\u1ec7n \u0111ang tr\u1ed1ng.",
                    Url = "/Teacher/Dashboard",
                    Count = 0,
                    IconCssClass = "fa-solid fa-circle-check"
                });
            }

            return items;
        }

        private async Task<List<ActivityLogViewModel>> GetRecentActivitiesAsync(int teacherId, List<int> subjectIds)
        {
            // Ghep hoat dong tu ket qua thi, de thi va hoc lieu de tao nhat ky gan day.
            var examResultLogs = await _context.ExamResults
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Subject)
                .Where(r => subjectIds.Contains(r.SubjectId))
                .OrderByDescending(r => r.FinishedAt ?? r.StartedAt)
                .Take(8)
                .Select(r => new ActivityLogViewModel
                {
                    Title = "Sinh vi\u00ean n\u1ed9p b\u00e0i",
                    Description = $"{r.User.FullName} \u0111\u00e3 n\u1ed9p b\u00e0i m\u00f4n {r.Subject.SubjectName}.",
                    CreatedAt = r.FinishedAt ?? r.StartedAt,
                    ActorName = r.User.FullName,
                    Type = "ExamResult",
                    IconCssClass = "fa-solid fa-file-circle-check"
                })
                .ToListAsync();

            var examLogs = await _context.Exams
                .AsNoTracking()
                .Include(e => e.Subject)
                .Where(e => e.TeacherId == teacherId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(6)
                .Select(e => new ActivityLogViewModel
                {
                    Title = "Gi\u1ea3ng vi\u00ean t\u1ea1o \u0111\u1ec1 thi",
                    Description = $"\u0110\u1ec1 \"{e.Title}\" thu\u1ed9c m\u00f4n {e.Subject.SubjectName}.",
                    CreatedAt = e.CreatedAt,
                    ActorName = "Gi\u1ea3ng vi\u00ean",
                    Type = "Exam",
                    IconCssClass = "fa-solid fa-file-lines"
                })
                .ToListAsync();

            var documentLogs = await _context.Documents
                .AsNoTracking()
                .Include(d => d.Subject)
                .Where(d => d.UploadedBy == teacherId && d.IsActive)
                .OrderByDescending(d => d.CreatedAt)
                .Take(6)
                .Select(d => new ActivityLogViewModel
                {
                    Title = "Gi\u1ea3ng vi\u00ean t\u1ea3i h\u1ecdc li\u1ec7u",
                    Description = $"T\u00e0i li\u1ec7u \"{d.Title}\" cho m\u00f4n {d.Subject.SubjectName}.",
                    CreatedAt = d.CreatedAt,
                    ActorName = "Gi\u1ea3ng vi\u00ean",
                    Type = "Document",
                    IconCssClass = "fa-solid fa-folder-open"
                })
                .ToListAsync();

            return examResultLogs
                .Concat(examLogs)
                .Concat(documentLogs)
                .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
                .Take(12)
                .ToList();
        }
    }
}
