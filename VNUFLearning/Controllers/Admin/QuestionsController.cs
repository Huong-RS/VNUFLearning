using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using VNUFLearning.Data;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class QuestionsController : Controller
    {
        private readonly VnufLearningContext _context;

        public QuestionsController(VnufLearningContext context)
        {
            _context = context;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        [HttpGet]
        [Route("~/Admin/Questions")]
        public async Task<IActionResult> Index(
            string? keyword,
            int? subjectId,
            int? questionType,
            int? level,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 10;

            var query = _context.Questions
                .Include(q => q.Subject)
                .Include(q => q.CreatedByUser)
                .Where(q => q.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(q =>
                    q.Content.Contains(keyword) ||
                    q.QuestionId.ToString().Contains(keyword));
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

            var totalItems = await query.CountAsync();

            var questions = await query
                .OrderByDescending(q => q.CreatedAt.HasValue)
                .ThenByDescending(q => q.CreatedAt)
                .ThenByDescending(q => q.QuestionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.SubjectList = new SelectList(
                await _context.Subjects.OrderBy(s => s.SubjectName).ToListAsync(),
                "SubjectId",
                "SubjectName",
                subjectId
            );

            ViewBag.QuestionType = questionType;
            ViewBag.Level = level;
            ViewBag.Keyword = keyword;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.SubjectId = subjectId;

            return View("~/Views/Admin/Questions/Index.cshtml", questions);
        }

        [HttpGet]
        [Route("~/Admin/Questions/Create")]
        public IActionResult Create()
        {
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "SubjectId", "SubjectName");
            return View("~/Views/Admin/Questions/Create.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Questions/Create")]
        public async Task<IActionResult> Create(Question question)
        {
            ModelState.Remove(nameof(question.Subject));
            ModelState.Remove(nameof(question.CreatedByUser));

            if (question.QuestionType == 2)
            {
                ModelState.Remove(nameof(question.OptionA));
                ModelState.Remove(nameof(question.OptionB));
                ModelState.Remove(nameof(question.OptionC));
                ModelState.Remove(nameof(question.OptionD));

                question.OptionA = null;
                question.OptionB = null;
                question.OptionC = null;
                question.OptionD = null;
            }
            else
            {
                ModelState.Remove(nameof(question.Explaination));
            }

            if (ModelState.IsValid)
            {
                question.CreatedAt = DateTime.Now;
                question.UpdatedAt = null;
                question.IsActive = true;

                var userIdClaim = User.FindFirst("UserId")?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    question.CreatedByUserId = userId;
                }

                _context.Add(question);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm câu hỏi thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewData["SubjectId"] = new SelectList(_context.Subjects, "SubjectId", "SubjectName", question.SubjectId);
            return View("~/Views/Admin/Questions/Create.cshtml", question);
        }

        [HttpGet]
        [Route("~/Admin/Questions/Edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var question = await _context.Questions.FindAsync(id);
            if (question == null) return NotFound();

            ViewData["SubjectId"] = new SelectList(_context.Subjects, "SubjectId", "SubjectName", question.SubjectId);
            return View("~/Views/Admin/Questions/Edit.cshtml", question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Questions/Edit/{id}")]
        public async Task<IActionResult> Edit(int id, Question question)
        {
            if (id != question.QuestionId) return NotFound();

            ModelState.Remove(nameof(question.Subject));
            ModelState.Remove(nameof(question.CreatedByUser));

            if (question.QuestionType == 2)
            {
                ModelState.Remove(nameof(question.OptionA));
                ModelState.Remove(nameof(question.OptionB));
                ModelState.Remove(nameof(question.OptionC));
                ModelState.Remove(nameof(question.OptionD));

                question.OptionA = null;
                question.OptionB = null;
                question.OptionC = null;
                question.OptionD = null;
            }
            else
            {
                ModelState.Remove(nameof(question.Explaination));
            }

            if (ModelState.IsValid)
            {
                try
                {
                    question.UpdatedAt = DateTime.Now;
                    _context.Update(question);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Cập nhật câu hỏi thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Questions.Any(e => e.QuestionId == question.QuestionId))
                        return NotFound();

                    throw;
                }
            }

            ViewData["SubjectId"] = new SelectList(_context.Subjects, "SubjectId", "SubjectName", question.SubjectId);
            return View("~/Views/Admin/Questions/Edit.cshtml", question);
        }

        [HttpGet]
        [Route("~/Admin/Questions/Delete/{id}")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var question = await _context.Questions
                .Include(q => q.Subject)
                .FirstOrDefaultAsync(m => m.QuestionId == id);

            if (question == null) return NotFound();

            return View("~/Views/Admin/Questions/Delete.cshtml", question);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Questions/Delete/{id}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var question = await _context.Questions.FindAsync(id);

            if (question != null)
            {
                question.IsActive = false;
                question.UpdatedAt = DateTime.Now;
                _context.Update(question);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã xóa câu hỏi!";
            }

            return RedirectToAction(nameof(Index));
        }
      
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Admin/Questions/Import")]
        public async Task<IActionResult> Import(int subjectId, IFormFile fileExcel)
        {
            if (subjectId <= 0)
            {
                TempData["Error"] = "Vui lòng chọn môn học.";
                return RedirectToAction(nameof(Index));
            }

            var subjectExists = await _context.Subjects.AnyAsync(s => s.SubjectId == subjectId);
            if (!subjectExists)
            {
                TempData["Error"] = "Môn học được chọn không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            if (fileExcel == null || fileExcel.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file Excel.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var stream = new MemoryStream();
                await fileExcel.CopyToAsync(stream);

                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets[0];

                if (worksheet == null || worksheet.Dimension == null)
                {
                    TempData["Error"] = "File Excel rỗng.";
                    return RedirectToAction(nameof(Index));
                }

                int rowCount = worksheet.Dimension.Rows;
                var listQuestions = new List<Question>();
                var errorRows = new List<string>();

                var userIdClaim = User.FindFirst("UserId")?.Value;
                int? createdByUserId = null;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    createdByUserId = userId;
                }

                for (int row = 2; row <= rowCount; row++)
                {
                    try
                    {
                        // CẤU TRÚC CỘT MỚI:
                        // A = Content
                        // B = QuestionType
                        // C = OptionA
                        // D = OptionB
                        // E = OptionC
                        // F = OptionD
                        // G = CorrectAnswer

                        var content = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                        var questionTypeText = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                        var optionA = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                        var optionB = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                        var optionC = worksheet.Cells[row, 5].Value?.ToString()?.Trim();
                        var optionD = worksheet.Cells[row, 6].Value?.ToString()?.Trim();
                        var correctAnswer = worksheet.Cells[row, 7].Value?.ToString()?.Trim();

                        // bỏ qua dòng trống
                        if (string.IsNullOrWhiteSpace(content) &&
                            string.IsNullOrWhiteSpace(questionTypeText) &&
                            string.IsNullOrWhiteSpace(optionA) &&
                            string.IsNullOrWhiteSpace(optionB) &&
                            string.IsNullOrWhiteSpace(optionC) &&
                            string.IsNullOrWhiteSpace(optionD) &&
                            string.IsNullOrWhiteSpace(correctAnswer))
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(content))
                        {
                            errorRows.Add($"Dòng {row}: Nội dung câu hỏi không được để trống.");
                            continue;
                        }

                        if (!int.TryParse(questionTypeText, out int questionType) || (questionType != 1 && questionType != 2))
                        {
                            errorRows.Add($"Dòng {row}: QuestionType chỉ được là 1 (Trắc nghiệm) hoặc 2 (Tự luận).");
                            continue;
                        }

                        var question = new Question
                        {
                            Content = content,
                            SubjectId = subjectId,
                            QuestionType = questionType,
                            Level = 1,
                            Chapter = null,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = null,
                            IsActive = true,
                            CreatedByUserId = createdByUserId
                        };

                        if (questionType == 1)
                        {
                            if (string.IsNullOrWhiteSpace(optionA) ||
                                string.IsNullOrWhiteSpace(optionB) ||
                                string.IsNullOrWhiteSpace(optionC) ||
                                string.IsNullOrWhiteSpace(optionD))
                            {
                                errorRows.Add($"Dòng {row}: Câu trắc nghiệm phải có đủ 4 đáp án A, B, C, D.");
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(correctAnswer))
                            {
                                errorRows.Add($"Dòng {row}: Câu trắc nghiệm phải có đáp án đúng.");
                                continue;
                            }

                            correctAnswer = correctAnswer.Trim().ToUpper();

                            if (correctAnswer != "A" &&
                                correctAnswer != "B" &&
                                correctAnswer != "C" &&
                                correctAnswer != "D")
                            {
                                errorRows.Add($"Dòng {row}: Đáp án đúng chỉ được là A, B, C hoặc D.");
                                continue;
                            }

                            question.OptionA = optionA;
                            question.OptionB = optionB;
                            question.OptionC = optionC;
                            question.OptionD = optionD;
                            question.CorrectAnswer = correctAnswer;
                            question.Explaination = null;
                        }
                        else
                        {
                            // Tự luận
                            question.OptionA = null;
                            question.OptionB = null;
                            question.OptionC = null;
                            question.OptionD = null;
                            question.CorrectAnswer = correctAnswer;
                            question.Explaination = null;
                        }

                        listQuestions.Add(question);
                    }
                    catch (Exception exRow)
                    {
                        errorRows.Add($"Dòng {row}: {exRow.Message}");
                    }
                }

                if (listQuestions.Any())
                {
                    await _context.Questions.AddRangeAsync(listQuestions);
                    await _context.SaveChangesAsync();
                }

                if (errorRows.Any() && listQuestions.Any())
                {
                    TempData["Success"] = $"Đã import thành công {listQuestions.Count} câu hỏi.";
                    TempData["Error"] = "Một số dòng bị lỗi: " + string.Join(" | ", errorRows.Take(10));
                }
                else if (errorRows.Any() && !listQuestions.Any())
                {
                    TempData["Error"] = "Import thất bại. " + string.Join(" | ", errorRows.Take(10));
                }
                else
                {
                    TempData["Success"] = $"Đã import {listQuestions.Count} câu hỏi thành công!";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi import: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        [Route("~/Admin/Questions/Export")]
        public IActionResult Export(string? keyword, int? subjectId, int? questionType, int? level)
        {
            var query = _context.Questions
                .Include(x => x.Subject)
                .Where(x => x.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(q =>
                    q.Content.Contains(keyword) ||
                    q.QuestionId.ToString().Contains(keyword));
            }

            if (subjectId.HasValue && subjectId.Value > 0)
            {
                query = query.Where(x => x.SubjectId == subjectId.Value);
            }

            if (questionType.HasValue && questionType.Value > 0)
            {
                query = query.Where(x => x.QuestionType == questionType.Value);
            }

            if (level.HasValue && level.Value > 0)
            {
                query = query.Where(x => x.Level == level.Value);
            }

            var data = query
                .OrderByDescending(x => x.QuestionId)
                .ToList();

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("DanhSachCauHoi");

            ws.Cells[1, 1].Value = "STT";
            ws.Cells[1, 2].Value = "ID";
            ws.Cells[1, 3].Value = "Nội dung";
            ws.Cells[1, 4].Value = "Môn học";
            ws.Cells[1, 5].Value = "Loại";
            ws.Cells[1, 6].Value = "Mức độ";
            ws.Cells[1, 7].Value = "Đáp án đúng";

            int row = 2;
            int stt = 1;

            foreach (var item in data)
            {
                ws.Cells[row, 1].Value = stt++;
                ws.Cells[row, 2].Value = item.QuestionId;
                ws.Cells[row, 3].Value = item.Content;
                ws.Cells[row, 4].Value = item.Subject?.SubjectName;
                ws.Cells[row, 5].Value = item.QuestionType == 1 ? "Trắc nghiệm" : "Tự luận";
                ws.Cells[row, 6].Value = item.Level switch
                {
                    1 => "Dễ",
                    2 => "Trung bình",
                    3 => "Khó",
                    _ => ""
                };
                ws.Cells[row, 7].Value = item.CorrectAnswer;
                row++;
            }

            ws.Cells.AutoFitColumns();

            var stream = new MemoryStream();
            package.SaveAs(stream);
            stream.Position = 0;

            return File(
                stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Admin_QuestionList_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }

        [HttpGet]
        [Route("~/Admin/Questions/DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            var filePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "templates",
                "MauImportCauHoi.xlsx"
            );

            if (!System.IO.File.Exists(filePath))
            {
                return Content("File mẫu chưa tồn tại trong wwwroot/templates");
            }

            var bytes = System.IO.File.ReadAllBytes(filePath);

            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "MauImportCauHoi.xlsx"
            );
        }
    }
}