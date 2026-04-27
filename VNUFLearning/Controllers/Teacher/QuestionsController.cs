using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]
    [Route("Teacher/[controller]/[action]")]
    public class QuestionsController : Controller
    {
        private readonly VnufLearningContext _context;

        public QuestionsController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Teacher/Questions")]
        public async Task<IActionResult> Index(string? keyword, int? subjectId, int? questionType, int? level)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return RedirectToAction("Login", "Account");

            var subjectIds = await GetAssignedSubjectIds(teacherId.Value);

            var query = _context.Questions
                .Include(q => q.Subject)
                .Where(q => q.IsActive && subjectIds.Contains(q.SubjectId))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(q => q.Content.Contains(keyword));
            }

            if (subjectId.HasValue && subjectId.Value > 0)
            {
                query = query.Where(q => q.SubjectId == subjectId.Value);
            }

            if (questionType.HasValue && questionType.Value > 0)
            {
                query = query.Where(q => q.QuestionType == questionType.Value);
            }

            if (level.HasValue && level.Value > 0)
            {
                query = query.Where(q => q.Level == level.Value);
            }

            var questions = await query
                .OrderByDescending(q => q.CreatedAt)
                .ThenByDescending(q => q.QuestionId)
                .ToListAsync();

            await LoadAssignedSubjects(teacherId.Value, subjectId);

            ViewBag.Keyword = keyword ?? "";
            ViewBag.QuestionType = questionType ?? 0;
            ViewBag.Level = level ?? 0;

            return View("~/Views/Teacher/Questions/Index.cshtml", questions);
        }

        [HttpGet]
        [Route("~/Teacher/Questions/Create")]
        public async Task<IActionResult> Create()
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return RedirectToAction("Login", "Account");

            await LoadAssignedSubjects(teacherId.Value);

            return View("~/Views/Teacher/Questions/Create.cshtml", new Question
            {
                QuestionType = 1,
                Level = 1,
                IsActive = true
            });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Questions/Create")]
        public async Task<IActionResult> Create(Question model)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return RedirectToAction("Login", "Account");

            // Tránh lỗi validate navigation property
            ModelState.Remove("Subject");
            ModelState.Remove("ExamDetails");
            ModelState.Remove("CreatedByUser");

            var canManage = await CanManageSubject(teacherId.Value, model.SubjectId);
            if (!canManage)
            {
                ModelState.AddModelError("SubjectId", "Bạn chưa được phân công môn học này.");
            }

            if (string.IsNullOrWhiteSpace(model.Content))
            {
                ModelState.AddModelError("Content", "Vui lòng nhập nội dung câu hỏi.");
            }

            if (model.QuestionType == 1)
            {
                if (string.IsNullOrWhiteSpace(model.OptionA) ||
                    string.IsNullOrWhiteSpace(model.OptionB) ||
                    string.IsNullOrWhiteSpace(model.OptionC) ||
                    string.IsNullOrWhiteSpace(model.OptionD))
                {
                    ModelState.AddModelError("", "Câu hỏi trắc nghiệm cần nhập đủ 4 đáp án A, B, C, D.");
                }

                if (string.IsNullOrWhiteSpace(model.CorrectAnswer))
                {
                    ModelState.AddModelError("CorrectAnswer", "Vui lòng nhập đáp án đúng.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadAssignedSubjects(teacherId.Value, model.SubjectId);
                return View("~/Views/Teacher/Questions/Create.cshtml", model);
            }

            model.Content = model.Content.Trim();
            model.CorrectAnswer = NormalizeAnswer(model.CorrectAnswer);
            model.CreatedByUserId = teacherId.Value;
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;
            model.IsActive = true;

            _context.Questions.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã thêm câu hỏi thành công.";
            return Redirect("/Teacher/Questions");
        }
        [HttpGet]
        [Route("~/Teacher/Questions/Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return RedirectToAction("Login", "Account");

            var question = await _context.Questions.FindAsync(id);
            if (question == null || !question.IsActive) return NotFound();

            var canManage = await CanManageSubject(teacherId.Value, question.SubjectId);
            if (!canManage) return Forbid();

            await LoadAssignedSubjects(teacherId.Value, question.SubjectId);

            return View("~/Views/Teacher/Questions/Edit.cshtml", question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Questions/Edit/{id}")]
        public async Task<IActionResult> Edit(int id, Question model)
        {
            var teacherId = GetTeacherId();
            if (!teacherId.HasValue)
                return Redirect("/Account/Login");

            if (id != model.QuestionId)
                return BadRequest();

            var question = await _context.Questions
                .FirstOrDefaultAsync(x => x.QuestionId == id);

            if (question == null)
                return NotFound();

            var canManageOldSubject = await _context.SubjectAssignments.AnyAsync(x =>
                x.TeacherId == teacherId.Value &&
                x.SubjectId == question.SubjectId);

            var canManageNewSubject = await _context.SubjectAssignments.AnyAsync(x =>
                x.TeacherId == teacherId.Value &&
                x.SubjectId == model.SubjectId);

            if (!canManageOldSubject || !canManageNewSubject)
                return Forbid();

            question.SubjectId = model.SubjectId;
            question.Content = model.Content;
            question.ImageUrl = model.ImageUrl;
            question.QuestionType = model.QuestionType;
            question.OptionA = model.OptionA;
            question.OptionB = model.OptionB;
            question.OptionC = model.OptionC;
            question.OptionD = model.OptionD;
            question.CorrectAnswer = model.CorrectAnswer;
            question.Explaination = model.Explaination;
            question.Level = model.Level;
            question.Chapter = model.Chapter;
            question.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật câu hỏi thành công.";
            return Redirect("/Teacher/Questions");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Questions/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return RedirectToAction("Login", "Account");

            var question = await _context.Questions.FindAsync(id);
            if (question == null) return NotFound();

            var canManage = await CanManageSubject(teacherId.Value, question.SubjectId);
            if (!canManage) return Forbid();

            question.IsActive = false;
            question.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa câu hỏi.";
            return Redirect("/Teacher/Questions");
        }
        [HttpGet]
        [Route("~/Teacher/Questions/DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Questions");

            ws.Cell(1, 1).Value = "Content";
            ws.Cell(1, 2).Value = "QuestionType";
            ws.Cell(1, 3).Value = "OptionA";
            ws.Cell(1, 4).Value = "OptionB";
            ws.Cell(1, 5).Value = "OptionC";
            ws.Cell(1, 6).Value = "OptionD";
            ws.Cell(1, 7).Value = "CorrectAnswer";
            ws.Cell(1, 8).Value = "Level";
            ws.Cell(1, 9).Value = "Chapter";
            ws.Cell(1, 10).Value = "Explaination";

            ws.Cell(2, 1).Value = "Khóa chính trong cơ sở dữ liệu dùng để làm gì?";
            ws.Cell(2, 2).Value = 1;
            ws.Cell(2, 3).Value = "Liên kết các bảng";
            ws.Cell(2, 4).Value = "Cho phép giá trị Null";
            ws.Cell(2, 5).Value = "Định danh duy nhất mỗi dòng";
            ws.Cell(2, 6).Value = "Sắp xếp dữ liệu";
            ws.Cell(2, 7).Value = "C";
            ws.Cell(2, 8).Value = 1;
            ws.Cell(2, 9).Value = "Chương 1";
            ws.Cell(2, 10).Value = "Primary Key dùng để định danh duy nhất bản ghi.";

            ws.Row(1).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "MauImportCauHoi_GiangVien.xlsx"
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Questions/ImportExcel")]
        public async Task<IActionResult> ImportExcel(IFormFile excelFile, int subjectId)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return RedirectToAction("Login", "Account");

            var canManage = await CanManageSubject(teacherId.Value, subjectId);
            if (!canManage)
            {
                TempData["Error"] = "Bạn không được import câu hỏi cho môn học này.";
                return Redirect("/Teacher/Questions");
            }

            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file Excel.";
                return Redirect("/Teacher/Questions");
            }

            var extension = Path.GetExtension(excelFile.FileName).ToLower();
            if (extension != ".xlsx")
            {
                TempData["Error"] = "Chỉ hỗ trợ file .xlsx.";
                return Redirect("/Teacher/Questions");
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
                var content = ws.Cell(row, 1).GetString().Trim();
                var questionTypeText = ws.Cell(row, 2).GetString().Trim();
                var optionA = ws.Cell(row, 3).GetString().Trim();
                var optionB = ws.Cell(row, 4).GetString().Trim();
                var optionC = ws.Cell(row, 5).GetString().Trim();
                var optionD = ws.Cell(row, 6).GetString().Trim();
                var correctAnswer = ws.Cell(row, 7).GetString().Trim();
                var levelText = ws.Cell(row, 8).GetString().Trim();
                var chapter = ws.Cell(row, 9).GetString().Trim();
                var explaination = ws.Cell(row, 10).GetString().Trim();

                if (string.IsNullOrWhiteSpace(content))
                {
                    errors.Add($"Dòng {row}: Nội dung câu hỏi không được để trống.");
                    continue;
                }

                int questionType = int.TryParse(questionTypeText, out var qt) ? qt : 1;
                int level = int.TryParse(levelText, out var lv) ? lv : 1;

                var question = new Question
                {
                    Content = content,
                    QuestionType = questionType,
                    OptionA = string.IsNullOrWhiteSpace(optionA) ? null : optionA,
                    OptionB = string.IsNullOrWhiteSpace(optionB) ? null : optionB,
                    OptionC = string.IsNullOrWhiteSpace(optionC) ? null : optionC,
                    OptionD = string.IsNullOrWhiteSpace(optionD) ? null : optionD,
                    CorrectAnswer = string.IsNullOrWhiteSpace(correctAnswer) ? null : correctAnswer,
                    Level = level,
                    Chapter = string.IsNullOrWhiteSpace(chapter) ? null : chapter,
                    Explaination = string.IsNullOrWhiteSpace(explaination) ? null : explaination,
                    SubjectId = subjectId,
                    CreatedByUserId = teacherId.Value,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Questions.Add(question);
                successCount++;
            }

            if (successCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = errors.Any()
                ? $"Import thành công {successCount} câu hỏi. Có lỗi: {string.Join(" | ", errors)}"
                : $"Import thành công {successCount} câu hỏi.";

            return Redirect("/Teacher/Questions");
        }

        private int? GetTeacherId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : null;
        }

        private async Task<List<int>> GetAssignedSubjectIds(int teacherId)
        {
            return await _context.SubjectAssignments
                .Where(sa => sa.TeacherId == teacherId)
                .Select(sa => sa.SubjectId)
                .ToListAsync();
        }

        private async Task<bool> CanManageSubject(int teacherId, int subjectId)
        {
            return await _context.SubjectAssignments
                .AnyAsync(sa => sa.TeacherId == teacherId && sa.SubjectId == subjectId);
        }

        private async Task LoadAssignedSubjects(int teacherId, int? selectedSubjectId = null)
        {
            var subjects = await _context.SubjectAssignments
                .Include(sa => sa.Subject)
                .Where(sa => sa.TeacherId == teacherId && sa.Subject.IsActive)
                .Select(sa => sa.Subject)
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            ViewBag.SubjectList = new SelectList(subjects, "SubjectId", "SubjectName", selectedSubjectId);
            ViewBag.Subjects = subjects;
        }
        private string? NormalizeAnswer(string? answer)
        {
            if (string.IsNullOrWhiteSpace(answer)) return null;

            answer = answer.Trim();

            return answer.ToUpper() switch
            {
                "OPTIONA" => "A",
                "OPTIONB" => "B",
                "OPTIONC" => "C",
                "OPTIOND" => "D",
                _ => answer.ToUpper()
            };
        }
    }
}