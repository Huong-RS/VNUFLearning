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
    public class ExamsController : Controller
    {
        private readonly VnufLearningContext _context;

        public ExamsController(VnufLearningContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("~/Teacher/Exams")]
        public async Task<IActionResult> Index(string? keyword, int? subjectId, string? status)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var query = _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.ExamQuestions)
                .Where(e => e.TeacherId == teacherId.Value)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(e => e.Title.Contains(keyword));
            }

            if (subjectId.HasValue && subjectId.Value > 0)
            {
                query = query.Where(e => e.SubjectId == subjectId.Value);
            }

            if (status == "published")
            {
                query = query.Where(e => e.IsPublished);
            }
            else if (status == "draft")
            {
                query = query.Where(e => !e.IsPublished);
            }

            var exams = await query
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            await LoadAssignedSubjects(teacherId.Value, subjectId);

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;

            return View("~/Views/Teacher/Exams/Index.cshtml", exams);
        }

        [HttpGet]
        [Route("~/Teacher/Exams/Create")]
        public async Task<IActionResult> Create()
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            await LoadAssignedSubjects(teacherId.Value);

            return View("~/Views/Teacher/Exams/Create.cshtml", new Exam
            {
                DurationMinutes = 45,
                TotalQuestions = 20,
                ExamType = 1,
                IsPublished = false
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Exams/Create")]
        public async Task<IActionResult> Create(Exam model)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            ModelState.Remove("Subject");
            ModelState.Remove("Teacher");
            ModelState.Remove("ExamQuestions");

            if (!await CanManageSubject(teacherId.Value, model.SubjectId))
            {
                ModelState.AddModelError("SubjectId", "Bạn không được tạo đề cho môn này.");
            }

            if (string.IsNullOrWhiteSpace(model.Title))
            {
                ModelState.AddModelError("Title", "Vui lòng nhập tên đề thi.");
            }

            if (model.DurationMinutes <= 0)
            {
                ModelState.AddModelError("DurationMinutes", "Thời gian làm bài phải lớn hơn 0.");
            }

            if (model.TotalQuestions <= 0)
            {
                ModelState.AddModelError("TotalQuestions", "Số câu hỏi phải lớn hơn 0.");
            }

            var questionQuery = _context.Questions
                .AsNoTracking()
                .Where(q => q.IsActive && q.SubjectId == model.SubjectId);

            if (model.ExamType == 1)
            {
                questionQuery = questionQuery.Where(q => q.QuestionType == 1);
            }
            else if (model.ExamType == 2)
            {
                questionQuery = questionQuery.Where(q => q.QuestionType == 2);
            }

            if (model.Level.HasValue && model.Level.Value > 0)
            {
                questionQuery = questionQuery.Where(q => q.Level == model.Level.Value);
            }

            if (!string.IsNullOrWhiteSpace(model.ChapterFrom))
            {
                questionQuery = questionQuery.Where(q =>
                    q.Chapter != null &&
                    string.Compare(q.Chapter, model.ChapterFrom) >= 0);
            }

            if (!string.IsNullOrWhiteSpace(model.ChapterTo))
            {
                questionQuery = questionQuery.Where(q =>
                    q.Chapter != null &&
                    string.Compare(q.Chapter, model.ChapterTo) <= 0);
            }

            var availableQuestionCount = await questionQuery.CountAsync();

            if (availableQuestionCount < model.TotalQuestions)
            {
                ModelState.AddModelError("", $"Chỉ có {availableQuestionCount} câu hỏi phù hợp, không đủ để tạo đề {model.TotalQuestions} câu.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAssignedSubjects(teacherId.Value, model.SubjectId);
                return View("~/Views/Teacher/Exams/Create.cshtml", model);
            }

            model.TeacherId = teacherId.Value;
            model.CreatedAt = DateTime.Now;

            _context.Exams.Add(model);
            await _context.SaveChangesAsync();

            var pickedQuestions = await questionQuery
                .OrderBy(q => Guid.NewGuid())
                .Take(model.TotalQuestions)
                .ToListAsync();

            int order = 1;

            foreach (var q in pickedQuestions)
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamId = model.ExamId,
                    QuestionId = q.QuestionId,
                    QuestionOrder = order++
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo đề thành công.";
            return Redirect("/Teacher/Exams");
        }

        [HttpGet]
        [Route("~/Teacher/Exams/DownloadTemplate")]
        public IActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("DeThi");

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
            ws.Cell(3, 1).Value = "Trình bày khái niệm Socket và vai trò của Socket trong lập trình mạng.";
            ws.Cell(3, 2).Value = 2;
            ws.Cell(3, 3).Value = "";
            ws.Cell(3, 4).Value = "";
            ws.Cell(3, 5).Value = "";
            ws.Cell(3, 6).Value = "";
            ws.Cell(3, 7).Value = "Socket là điểm cuối của một kết nối truyền thông giữa hai tiến trình qua mạng, thường gắn với địa chỉ IP và cổng, dùng để gửi/nhận dữ liệu giữa client và server.";
            ws.Cell(3, 8).Value = 2;
            ws.Cell(3, 9).Value = "Chương 1";
            ws.Cell(3, 10).Value = "Rubric: Socket là endpoint giao tiếp mạng 25%; gắn với IP và Port 20%; gửi/nhận dữ liệu client-server 30%; hỗ trợ TCP/UDP 15%; có ví dụ ứng dụng 10%.";

            ws.Row(1).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "MauImportDeThi.xlsx"
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Exams/Import")]
        public async Task<IActionResult> Import(IFormFile file, string title, int subjectId, int durationMinutes, int examType = 3)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Chưa chọn file Excel.";
                return Redirect("/Teacher/Exams/Create");
            }

            if (!await CanManageSubject(teacherId.Value, subjectId))
            {
                TempData["Error"] = "Bạn không có quyền với môn này.";
                return Redirect("/Teacher/Exams");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "Vui lòng nhập tên đề thi.";
                return Redirect("/Teacher/Exams/Create");
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheet(1);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

            if (lastRow < 2)
            {
                TempData["Error"] = "File Excel chưa có dữ liệu câu hỏi.";
                return Redirect("/Teacher/Exams/Create");
            }

            var exam = new Exam
            {
                Title = title.Trim(),
                SubjectId = subjectId,
                TeacherId = teacherId.Value,
                DurationMinutes = durationMinutes <= 0 ? 45 : durationMinutes,
                TotalQuestions = lastRow - 1,
                ExamType = examType,
                CreatedAt = DateTime.Now,
                IsPublished = false
            };

            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            int order = 1;
            int success = 0;

            for (int row = 2; row <= lastRow; row++)
            {
                var content = ws.Cell(row, 1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(content)) continue;

                var questionType = int.TryParse(ws.Cell(row, 2).GetString().Trim(), out var qt) ? qt : 1;
                var level = int.TryParse(ws.Cell(row, 8).GetString().Trim(), out var lv) ? lv : 1;

                var rawCorrectAnswer = ws.Cell(row, 7).GetString().Trim();
                var rawExplaination = ws.Cell(row, 10).GetString().Trim();

                var question = new Question
                {
                    Content = content,
                    QuestionType = questionType,

                    OptionA = questionType == 1 ? ws.Cell(row, 3).GetString().Trim() : null,
                    OptionB = questionType == 1 ? ws.Cell(row, 4).GetString().Trim() : null,
                    OptionC = questionType == 1 ? ws.Cell(row, 5).GetString().Trim() : null,
                    OptionD = questionType == 1 ? ws.Cell(row, 6).GetString().Trim() : null,

                    // Trắc nghiệm: A/B/C/D
                    // Tự luận: đáp án mẫu giữ nguyên văn
                    CorrectAnswer = questionType == 1
                        ? NormalizeAnswer(rawCorrectAnswer)
                        : rawCorrectAnswer,

                    // Rubric chấm điểm cho Gemini
                    Explaination = rawExplaination,

                    Level = level,
                    Chapter = ws.Cell(row, 9).GetString().Trim(),
                    SubjectId = subjectId,
                    CreatedByUserId = teacherId.Value,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Questions.Add(question);
                await _context.SaveChangesAsync();

                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamId = exam.ExamId,
                    QuestionId = question.QuestionId,
                    QuestionOrder = order++
                });

                success++;
            }

            exam.TotalQuestions = success;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Import đề thành công, đã thêm {success} câu hỏi.";
            return Redirect("/Teacher/Exams");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Exams/Publish/{id}")]
        public async Task<IActionResult> Publish(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var exam = await _context.Exams
                .Include(e => e.ExamQuestions)
                .FirstOrDefaultAsync(e => e.ExamId == id && e.TeacherId == teacherId.Value);

            if (exam == null) return NotFound();

            if (exam.ExamQuestions == null || !exam.ExamQuestions.Any())
            {
                TempData["Error"] = "Không thể công khai đề thi vì đề chưa có câu hỏi.";
                return Redirect("/Teacher/Exams");
            }

            exam.IsPublished = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đề thi đã được công khai.";
            return Redirect("/Teacher/Exams");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Exams/Unpublish/{id}")]
        public async Task<IActionResult> Unpublish(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.ExamId == id && e.TeacherId == teacherId.Value);
            if (exam == null) return NotFound();

            exam.IsPublished = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đề thi đã chuyển về trạng thái nháp.";
            return Redirect("/Teacher/Exams");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Exams/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var exam = await _context.Exams
                .Include(e => e.ExamQuestions)
                .FirstOrDefaultAsync(e => e.ExamId == id && e.TeacherId == teacherId.Value);

            if (exam == null) return NotFound();

            _context.ExamQuestions.RemoveRange(exam.ExamQuestions);
            _context.Exams.Remove(exam);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa đề thi.";
            return Redirect("/Teacher/Exams");
        }

        private int? GetTeacherId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : null;
        }

        private async Task<bool> CanManageSubject(int teacherId, int subjectId)
        {
            if (subjectId <= 0) return false;

            return await _context.SubjectAssignments
                .AsNoTracking()
                .AnyAsync(x => x.TeacherId == teacherId && x.SubjectId == subjectId);
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
        [HttpGet]
        [Route("~/Teacher/Exams/ManageQuestions/{id}")]
        public async Task<IActionResult> ManageQuestions(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var exam = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.ExamQuestions)
                    .ThenInclude(eq => eq.Question)
                .FirstOrDefaultAsync(e => e.ExamId == id && e.TeacherId == teacherId.Value);

            if (exam == null) return NotFound();

            return View("~/Views/Teacher/Exams/ManageQuestions.cshtml", exam);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Exams/RemoveQuestion")]
        public async Task<IActionResult> RemoveQuestion(int examQuestionId)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var examQuestion = await _context.ExamQuestions
                .Include(eq => eq.Exam)
                .FirstOrDefaultAsync(eq => eq.ExamQuestionId == examQuestionId &&
                                           eq.Exam.TeacherId == teacherId.Value);

            if (examQuestion == null) return NotFound();

            var examId = examQuestion.ExamId;

            _context.ExamQuestions.Remove(examQuestion);
            await _context.SaveChangesAsync();

            var exam = await _context.Exams.FindAsync(examId);
            if (exam != null)
            {
                exam.TotalQuestions = await _context.ExamQuestions.CountAsync(x => x.ExamId == examId);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Đã loại câu hỏi khỏi đề.";
            return Redirect($"/Teacher/Exams/ManageQuestions/{examId}");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("~/Teacher/Exams/AddRandomQuestions")]
        public async Task<IActionResult> AddRandomQuestions(int examId, int quantity)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var exam = await _context.Exams
                .Include(e => e.ExamQuestions)
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.TeacherId == teacherId.Value);

            if (exam == null) return NotFound();

            if (quantity <= 0)
            {
                TempData["Error"] = "Số câu thêm phải lớn hơn 0.";
                return Redirect($"/Teacher/Exams/ManageQuestions/{examId}");
            }

            var existedQuestionIds = exam.ExamQuestions
                .Select(x => x.QuestionId)
                .ToList();

            var query = _context.Questions
                .AsNoTracking()
                .Where(q => q.IsActive &&
                            q.SubjectId == exam.SubjectId &&
                            !existedQuestionIds.Contains(q.QuestionId));

            if (exam.ExamType == 1)
            {
                query = query.Where(q => q.QuestionType == 1);
            }
            else if (exam.ExamType == 2)
            {
                query = query.Where(q => q.QuestionType == 2);
            }

            if (exam.Level.HasValue && exam.Level.Value > 0)
            {
                query = query.Where(q => q.Level == exam.Level.Value);
            }

            if (!string.IsNullOrWhiteSpace(exam.ChapterFrom))
            {
                query = query.Where(q => q.Chapter != null &&
                                         string.Compare(q.Chapter, exam.ChapterFrom) >= 0);
            }

            if (!string.IsNullOrWhiteSpace(exam.ChapterTo))
            {
                query = query.Where(q => q.Chapter != null &&
                                         string.Compare(q.Chapter, exam.ChapterTo) <= 0);
            }

            var questions = await query
                .OrderBy(q => Guid.NewGuid())
                .Take(quantity)
                .ToListAsync();

            if (!questions.Any())
            {
                TempData["Error"] = "Không còn câu hỏi phù hợp để thêm.";
                return Redirect($"/Teacher/Exams/ManageQuestions/{examId}");
            }

            var maxOrder = exam.ExamQuestions.Any()
                ? exam.ExamQuestions.Max(x => x.QuestionOrder ?? 0)
                : 0;

            foreach (var q in questions)
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamId = examId,
                    QuestionId = q.QuestionId,
                    QuestionOrder = ++maxOrder
                });
            }

            exam.TotalQuestions = exam.ExamQuestions.Count + questions.Count;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm ngẫu nhiên {questions.Count} câu hỏi.";
            return Redirect($"/Teacher/Exams/ManageQuestions/{examId}");
        }
    }
}