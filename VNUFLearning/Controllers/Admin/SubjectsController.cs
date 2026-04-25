using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;
using VNUFLearning.Models.ViewModels;
using ClosedXML.Excel;

namespace VNUFLearning.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class SubjectsController : Controller
    {
        private readonly VnufLearningContext _context;
        private readonly IWebHostEnvironment _environment;

        public SubjectsController(VnufLearningContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        [Route("~/Admin/Subjects")]
        public async Task<IActionResult> Index(string? keyword, string? department)
        {
            var query = _context.Subjects
                .Include(s => s.SubjectAssignments)
                    .ThenInclude(sa => sa.Teacher)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(s =>
                    s.SubjectName.Contains(keyword) ||
                    (s.SubjectCode != null && s.SubjectCode.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                query = query.Where(s => s.DepartmentName == department);
            }

            var subjects = await query
                .OrderBy(s => s.SubjectId)
                .ToListAsync();

            ViewBag.Keyword = keyword ?? "";
            ViewBag.Department = department ?? "";

            ViewBag.Departments = await _context.Subjects
                .Where(s => !string.IsNullOrWhiteSpace(s.DepartmentName))
                .Select(s => s.DepartmentName!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return View("~/Views/Admin/Subjects/Index.cshtml", subjects);
        }

       [HttpGet]
[Route("~/Admin/Subjects/Create")]
public async Task<IActionResult> Create()
{
    ViewBag.Teachers = await _context.Users
        .Include(u => u.Role)
        .Where(u => u.Role.RoleName == "GiangVien" && u.IsActive)
        .OrderBy(u => u.FullName)
        .ToListAsync();

    return View("~/Views/Admin/Subjects/Create.cshtml", new Subject
    {
        IsActive = true,
        Credits = 3
    });
}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Subjects/Create")]
        public async Task<IActionResult> Create(Subject model, IFormFile? coverImage, List<int>? selectedTeacherIds)
        {
            ViewBag.Teachers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "GiangVien" && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

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
            model.SubjectCode = string.IsNullOrWhiteSpace(model.SubjectCode) ? null : model.SubjectCode.Trim();
            model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            model.DepartmentName = string.IsNullOrWhiteSpace(model.DepartmentName) ? null : model.DepartmentName.Trim();
            model.CoverImageUrl = await SaveCoverImageAsync(coverImage) ?? "/images/default-subject.jpg";
            model.IsActive = true;

            _context.Subjects.Add(model);
            await _context.SaveChangesAsync();

            if (selectedTeacherIds != null && selectedTeacherIds.Any())
            {
                foreach (var teacherId in selectedTeacherIds.Distinct())
                {
                    _context.SubjectAssignments.Add(new SubjectAssignment
                    {
                        SubjectId = model.SubjectId,
                        TeacherId = teacherId,
                        AssignedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"Đã thêm môn học {model.SubjectName} thành công.";
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        [Route("~/Admin/Subjects/DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Subjects");

            ws.Cell(1, 1).Value = "SubjectCode";
            ws.Cell(1, 2).Value = "SubjectName";
            ws.Cell(1, 3).Value = "Credits";
            ws.Cell(1, 4).Value = "DepartmentName";
            ws.Cell(1, 5).Value = "Description";

            ws.Cell(2, 1).Value = "IT3020";
            ws.Cell(2, 2).Value = "Lập trình mạng";
            ws.Cell(2, 3).Value = 3;
            ws.Cell(2, 4).Value = "Khoa CNTT";
            ws.Cell(2, 5).Value = "Môn học về lập trình mạng cơ bản.";

            ws.Row(1).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Mau_Import_MonHoc.xlsx"
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Subjects/ImportExcel")]
        public async Task<IActionResult> ImportExcel(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Success"] = "Vui lòng chọn file Excel.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(excelFile.FileName).ToLower();
            if (extension != ".xlsx")
            {
                TempData["Success"] = "Chỉ hỗ trợ file .xlsx.";
                return RedirectToAction(nameof(Index));
            }

            var successCount = 0;
            var errors = new List<string>();

            using var stream = new MemoryStream();
            await excelFile.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

            for (int row = 2; row <= lastRow; row++)
            {
                var subjectCode = ws.Cell(row, 1).GetString().Trim();
                var subjectName = ws.Cell(row, 2).GetString().Trim();
                var creditsText = ws.Cell(row, 3).GetString().Trim();
                var departmentName = ws.Cell(row, 4).GetString().Trim();
                var description = ws.Cell(row, 5).GetString().Trim();

                if (string.IsNullOrWhiteSpace(subjectName))
                {
                    errors.Add($"Dòng {row}: Tên môn học không được để trống.");
                    continue;
                }

                var existed = await _context.Subjects.AnyAsync(s =>
                    s.SubjectName == subjectName ||
                    (!string.IsNullOrWhiteSpace(subjectCode) && s.SubjectCode == subjectCode));

                if (existed)
                {
                    errors.Add($"Dòng {row}: Môn học hoặc mã học phần đã tồn tại.");
                    continue;
                }

                int? credits = null;
                if (int.TryParse(creditsText, out var c))
                {
                    credits = c;
                }

                var subject = new Subject
                {
                    SubjectCode = string.IsNullOrWhiteSpace(subjectCode) ? null : subjectCode,
                    SubjectName = subjectName,
                    Credits = credits,
                    DepartmentName = string.IsNullOrWhiteSpace(departmentName) ? null : departmentName,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    CoverImageUrl = "/images/default-subject.jpg",
                    IsActive = true
                };

                _context.Subjects.Add(subject);
                successCount++;
            }

            if (successCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = errors.Any()
                ? $"Import thành công {successCount} môn học. Có lỗi: {string.Join(" | ", errors)}"
                : $"Import thành công {successCount} môn học.";

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
        public async Task<IActionResult> Edit(int id, Subject model, IFormFile? coverImage)
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
            subject.SubjectCode = string.IsNullOrWhiteSpace(model.SubjectCode) ? null : model.SubjectCode.Trim();
            subject.Credits = model.Credits;
            subject.DepartmentName = string.IsNullOrWhiteSpace(model.DepartmentName) ? null : model.DepartmentName.Trim();
            subject.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            subject.IsActive = model.IsActive;

            var newImageUrl = await SaveCoverImageAsync(coverImage);
            if (!string.IsNullOrWhiteSpace(newImageUrl))
            {
                subject.CoverImageUrl = newImageUrl;
            }

            _context.Subjects.Update(subject);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật môn học {subject.SubjectName} thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("~/Admin/Subjects/AssignTeacher/{id}")]
        public async Task<IActionResult> AssignTeacher(int id)
        {
            var subject = await _context.Subjects
                .Include(s => s.SubjectAssignments)
                .FirstOrDefaultAsync(s => s.SubjectId == id);

            if (subject == null)
            {
                TempData["Success"] = "Không tìm thấy môn học.";
                return RedirectToAction(nameof(Index));
            }

            var teachers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "GiangVien" && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var assignedTeacherIds = subject.SubjectAssignments
                .Select(sa => sa.TeacherId)
                .ToList();

            var assignedTeachers = await _context.Users
                .Where(u => assignedTeacherIds.Contains(u.UserId))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var model = new AssignTeacherViewModel
            {
                SubjectId = subject.SubjectId,
                SubjectName = subject.SubjectName,
                SelectedTeacherIds = assignedTeacherIds,
                AvailableTeachers = teachers,
                AssignedTeachers = assignedTeachers
            };

            return View("~/Views/Admin/Subjects/AssignTeacher.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Subjects/AssignTeacher/{id}")]
        public async Task<IActionResult> AssignTeacher(int id, AssignTeacherViewModel model)
        {
            if (id != model.SubjectId)
            {
                return BadRequest();
            }

            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
            {
                TempData["Success"] = "Không tìm thấy môn học.";
                return RedirectToAction(nameof(Index));
            }

            var oldAssignments = await _context.SubjectAssignments
                .Where(sa => sa.SubjectId == id)
                .ToListAsync();

            _context.SubjectAssignments.RemoveRange(oldAssignments);

            if (model.SelectedTeacherIds != null && model.SelectedTeacherIds.Any())
            {
                foreach (var teacherId in model.SelectedTeacherIds.Distinct())
                {
                    _context.SubjectAssignments.Add(new SubjectAssignment
                    {
                        SubjectId = id,
                        TeacherId = teacherId,
                        AssignedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật phân công giảng viên cho môn {subject.SubjectName}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject != null)
            {
                subject.IsActive = !subject.IsActive;
                await _context.SaveChangesAsync();

                TempData["Success"] = subject.IsActive
                    ? $"Đã mở môn học {subject.SubjectName}."
                    : $"Đã tạm ẩn môn học {subject.SubjectName}.";
            }

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

        private async Task<string?> SaveCoverImageAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                return null;
            }

            var uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "subjects");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/subjects/{fileName}";
        }
    }
}