using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class SubjectsController : Controller
    {
        private readonly VnufLearningContext _context;

        public SubjectsController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Admin/Subjects")]
        public async Task<IActionResult> Index()
        {
            var subjects = await _context.Subjects
                .OrderByDescending(s => s.SubjectId)
                .ToListAsync();

            return View("~/Views/Admin/Subjects/Index.cshtml", subjects);
        }

        [HttpGet]
        [Route("~/Admin/Subjects/Create")]
        public IActionResult Create()
        {
            return View("~/Views/Admin/Subjects/Create.cshtml", new Subject());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Subjects/Create")]
        public async Task<IActionResult> Create(Subject model)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/Subjects/Create.cshtml", model);
            }

            var subjectName = model.SubjectName?.Trim();

            if (string.IsNullOrWhiteSpace(subjectName))
            {
                ModelState.AddModelError("SubjectName", "Vui lòng nhập tên môn học.");
                return View("~/Views/Admin/Subjects/Create.cshtml", model);
            }

            var existed = await _context.Subjects.AnyAsync(s => s.SubjectName == subjectName);
            if (existed)
            {
                ModelState.AddModelError("SubjectName", "Tên môn học đã tồn tại.");
                return View("~/Views/Admin/Subjects/Create.cshtml", model);
            }

            model.SubjectName = subjectName;
            model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

            _context.Subjects.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm môn học {model.SubjectName} thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("~/Admin/Subjects/Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
            {
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/Admin/Subjects/Edit.cshtml", subject);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Subjects/Edit/{id}")]
        public async Task<IActionResult> Edit(int id, Subject model)
        {
            if (id != model.SubjectId)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/Subjects/Edit.cshtml", model);
            }

            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var subjectName = model.SubjectName?.Trim();

            if (string.IsNullOrWhiteSpace(subjectName))
            {
                ModelState.AddModelError("SubjectName", "Vui lòng nhập tên môn học.");
                return View("~/Views/Admin/Subjects/Edit.cshtml", model);
            }

            var existed = await _context.Subjects
                .AnyAsync(s => s.SubjectName == subjectName && s.SubjectId != id);

            if (existed)
            {
                ModelState.AddModelError("SubjectName", "Tên môn học đã tồn tại.");
                return View("~/Views/Admin/Subjects/Edit.cshtml", model);
            }

            subject.SubjectName = subjectName;
            subject.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

            _context.Subjects.Update(subject);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật môn học {subject.SubjectName} thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject != null)
            {
                _context.Subjects.Remove(subject);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xóa môn học {subject.SubjectName}.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}