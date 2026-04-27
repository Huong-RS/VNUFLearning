using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VNUFLearning.Data;

namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]
    [Route("Teacher/[controller]/[action]")]
    public class SubjectsController : Controller
    {
        private readonly VnufLearningContext _context;

        public SubjectsController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Teacher/Subjects")]
        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out int teacherId))
            {
                return RedirectToAction("Login", "Account");
            }

            var subjects = await _context.SubjectAssignments
                .Include(sa => sa.Subject)
                .Where(sa => sa.TeacherId == teacherId && sa.Subject.IsActive)
                .Select(sa => sa.Subject)
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            return View("~/Views/Teacher/Subjects/Index.cshtml", subjects);
        }
    }
}