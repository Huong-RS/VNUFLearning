using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;
using OfficeOpenXml;
using System.IO;


namespace VNUFLearning.Controllers.Teacher
{
    public class QuestionsController : Controller
    {
        private readonly VnufLearningContext _context;

        public QuestionsController(VnufLearningContext context)
        {
            _context = context;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public IActionResult Export(int? subjectId)
        {
            var query = _context.Questions
                        .Include(x => x.Subject)
                        .AsQueryable();

            // LỌC THEO MÔN HỌC
            if (subjectId.HasValue && subjectId.Value > 0)
            {
                query = query.Where(x => x.SubjectId == subjectId.Value);
            }

            var data = query.ToList();

            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("DanhSach");

                ws.Cells[1, 1].Value = "STT";
                ws.Cells[1, 2].Value = "Content";
                ws.Cells[1, 3].Value = "Subject";
                ws.Cells[1, 4].Value = "Type";
                ws.Cells[1, 5].Value = "Correct Answer";

                int row = 2;
                int stt = 1;

                foreach (var item in data)
                {
                    ws.Cells[row, 1].Value = stt++;
                    ws.Cells[row, 2].Value = item.Content;
                    ws.Cells[row, 3].Value = item.Subject?.SubjectName;
                    ws.Cells[row, 4].Value = item.QuestionType == 1 ? "Multiple Choice" : "Essay";
                    ws.Cells[row, 5].Value = item.CorrectAnswer;
                    row++;
                }

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "QuestionList.xlsx");
            }
        }



        // 2. MỞ FORM TẠO MỚI
        public IActionResult Create()
        {
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "SubjectId", "SubjectName");
            return View();
        }

        // 3. XỬ LÝ LƯU (CREATE)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Question question)
        {
            // Bỏ qua check Subject Object
            ModelState.Remove(nameof(question.Subject));

            if (question.QuestionType == 2) // Tự luận
            {
                // Bỏ qua lỗi các trường trắc nghiệm
                ModelState.Remove(nameof(question.OptionA));
                ModelState.Remove(nameof(question.OptionB));
                ModelState.Remove(nameof(question.OptionC));
                ModelState.Remove(nameof(question.OptionD));

                // Xóa dữ liệu rác nếu có
                question.OptionA = null; question.OptionB = null;
                question.OptionC = null; question.OptionD = null;
            }
            else // Trắc nghiệm
            {
                // Giả sử có trường Explaination, nếu không dùng thì bỏ qua
                ModelState.Remove("Explaination");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(question);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Thêm thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi lưu: " + ex.Message);
                }
            }

            // Load lại dropdown nếu lỗi
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "SubjectId", "SubjectName", question.SubjectId);
            return View(question);
        }

        // 4. IMPORT EXCEL
        [HttpPost]
        public async Task<IActionResult> Import(IFormFile fileExcel)
        {
            if (fileExcel == null || fileExcel.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn file!";
                return RedirectToAction(nameof(Create));
            }

            try
            {
                using (var stream = new MemoryStream())
                {
                    await fileExcel.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        if (worksheet == null || worksheet.Dimension == null)
                        {
                            TempData["Error"] = "File rỗng!";
                            return RedirectToAction(nameof(Create));
                        }

                        int rowCount = worksheet.Dimension.Rows;
                        var listQuestions = new List<Question>();

                        // Lấy danh sách ID môn học hiện có
                        var existingSubjectIds = await _context.Subjects.Select(s => s.SubjectId).ToListAsync();
                        int defaultSubjectId = existingSubjectIds.FirstOrDefault();

                        // Chạy từ dòng 2 (Bỏ qua tiêu đề)
                        for (int row = 2; row <= rowCount; row++)
                        {
                            var content = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(content)) continue;

                            // Dùng TryParse để tránh lỗi format
                            int qType = 1;
                            int.TryParse(worksheet.Cells[row, 2].Value?.ToString(), out qType);

                            int subId = defaultSubjectId;
                            if (int.TryParse(worksheet.Cells[row, 8].Value?.ToString(), out int parsedId))
                            {
                                if (existingSubjectIds.Contains(parsedId)) subId = parsedId;
                            }

                            var q = new Question
                            {
                                Content = content,
                                QuestionType = qType,
                                SubjectId = subId > 0 ? subId : 1,
                                Level = 1,
                                CorrectAnswer = worksheet.Cells[row, 7].Value?.ToString()
                            };

                            if (qType == 1) // Chỉ lấy đáp án nhiễu nếu là TN
                            {
                                q.OptionA = worksheet.Cells[row, 3].Value?.ToString();
                                q.OptionB = worksheet.Cells[row, 4].Value?.ToString();
                                q.OptionC = worksheet.Cells[row, 5].Value?.ToString();
                                q.OptionD = worksheet.Cells[row, 6].Value?.ToString();
                            }
                            listQuestions.Add(q);
                        }

                        if (listQuestions.Any())
                        {
                            await _context.Questions.AddRangeAsync(listQuestions);
                            await _context.SaveChangesAsync();
                            TempData["Success"] = $"Đã nhập {listQuestions.Count} câu hỏi!";
                        }
                        else { TempData["Error"] = "Không tìm thấy dữ liệu hợp lệ."; }
                    }
                }
            }
            catch (Exception ex) { TempData["Error"] = "Lỗi file: " + ex.Message; }

            return RedirectToAction(nameof(Index));
        }


        // 3. EDIT (SỬA CÂU HỎI) - MỚI THÊM
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var question = await _context.Questions.FindAsync(id);
            if (question == null) return NotFound();
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "SubjectId", "SubjectName", question.SubjectId);
            return View(question);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Question question)
        {
            if (id != question.QuestionId) return NotFound();

            ModelState.Remove(nameof(question.Subject));
            // Logic validation giống Create
            if (question.QuestionType == 2)
            {
                ModelState.Remove(nameof(question.OptionA)); ModelState.Remove(nameof(question.OptionB));
                ModelState.Remove(nameof(question.OptionC)); ModelState.Remove(nameof(question.OptionD));
                question.OptionA = null; question.OptionB = null; question.OptionC = null; question.OptionD = null;
            }
            else { ModelState.Remove(nameof(question.Explaination)); }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(question);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Questions.Any(e => e.QuestionId == question.QuestionId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["SubjectId"] = new SelectList(_context.Subjects, "SubjectId", "SubjectName", question.SubjectId);
            return View(question);
        }

        // 4. DELETE (XÓA CÂU HỎI) - MỚI THÊM
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var question = await _context.Questions.Include(q => q.Subject).FirstOrDefaultAsync(m => m.QuestionId == id);
            if (question == null) return NotFound();
            return View(question);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question != null)
            {
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa câu hỏi!";
            }
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Index(string keyword, int? subjectId)
        {
            var query = _context.Questions
                                .Include(q => q.Subject)
                                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(q => q.Content.Contains(keyword));
            }

            if (subjectId.HasValue)
            {
                query = query.Where(q => q.SubjectId == subjectId);
            }

            var result = await query
                .OrderBy(q => q.Subject.SubjectName)
                .ThenByDescending(q => q.QuestionId)
                .ToListAsync();

            ViewBag.SubjectList = new SelectList(_context.Subjects, "SubjectId", "SubjectName");

            return View(result);
        }



        public IActionResult DownloadTemplate()
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(),

                                        "templates",
                                        "MauImportCauHoi.xlsx");

            if (!System.IO.File.Exists(filePath))
            {
                return Content("File mẫu chưa tồn tại trong wwwroot/templates");
            }

            var bytes = System.IO.File.ReadAllBytes(filePath);

            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "MauImportCauHoi.xlsx");
        }

    }
}