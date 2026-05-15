using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;
using VNUFLearning.Services.Storage;

namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]
    [Route("Teacher/[controller]/[action]")]
    public class ExamsController : Controller
    {
        private readonly VnufLearningContext _context;
        private readonly IMinioService _minioService;

        public ExamsController(
            VnufLearningContext context,
            IMinioService minioService)
        {
            _context = context;
            _minioService = minioService;
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

            StorageUploadResult sourceFile;

            try
            {
                sourceFile = await UploadGeneratedExamFileAsync(model, pickedQuestions);
            }
            catch (Exception ex)
            {
                _context.Exams.Remove(model);
                await _context.SaveChangesAsync();

                TempData["Error"] = $"Tạo file đề thi trên MinIO thất bại: {ex.Message}";
                return Redirect("/Teacher/Exams/Create");
            }

            model.SourceFileUrl = sourceFile.Url;
            model.SourceFileObjectName = sourceFile.ObjectName;
            model.SourceFileName = sourceFile.OriginalFileName;
            model.SourceFileType = "XLSX";
            model.SourceFileSize = sourceFile.Size;

            int order = 1;
            var defaultQuestionScore = pickedQuestions.Any()
                ? Math.Round(10.0 / pickedQuestions.Count, 4)
                : 0;

            foreach (var q in pickedQuestions)
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamId = model.ExamId,
                    QuestionId = q.QuestionId,
                    QuestionOrder = order++,
                    Score = defaultQuestionScore
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
            ws.Cell(1, 11).Value = "Score";

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
            ws.Cell(2, 11).Value = 5;
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
            ws.Cell(3, 11).Value = 5;

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

            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Chỉ cho phép import file Excel (.xlsx, .xls).";
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

            StorageUploadResult sourceFile;

            try
            {
                sourceFile = await _minioService.UploadAsync(
                    file,
                    "exams/imports",
                    allowedExtensions,
                    20 * 1024 * 1024);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lưu file đề thi lên MinIO thất bại: {ex.Message}";
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
                IsPublished = false,
                SourceFileUrl = sourceFile.Url,
                SourceFileObjectName = sourceFile.ObjectName,
                SourceFileName = sourceFile.OriginalFileName,
                SourceFileType = extension.Replace(".", "").ToUpper(),
                SourceFileSize = sourceFile.Size
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
                var questionScore = double.TryParse(ws.Cell(row, 11).GetString().Trim(), out var sc) && sc > 0
                    ? sc
                    : (double?)null;

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
                    QuestionOrder = order++,
                    Score = questionScore
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

            await _minioService.DeleteAsync(exam.SourceFileObjectName ?? string.Empty);

            _context.ExamQuestions.RemoveRange(exam.ExamQuestions);
            _context.Exams.Remove(exam);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa đề thi.";
            return Redirect("/Teacher/Exams");
        }

        [HttpGet]
        [Route("~/Teacher/Exams/DownloadSourceFile/{id}")]
        public async Task<IActionResult> DownloadSourceFile(int id)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var exam = await _context.Exams
                .FirstOrDefaultAsync(e => e.ExamId == id && e.TeacherId == teacherId.Value);

            if (exam == null) return NotFound();

            if (string.IsNullOrWhiteSpace(exam.SourceFileUrl))
            {
                TempData["Error"] = "Đề thi này chưa có file nguồn trên MinIO.";
                return Redirect("/Teacher/Exams");
            }

            return Redirect(exam.SourceFileUrl);
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

        private async Task<StorageUploadResult> UploadGeneratedExamFileAsync(Exam exam, List<Question> questions)
        {
            var fileName = $"{SanitizeFileName(exam.Title)}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            using var fileStream = BuildGeneratedExamWorkbookStream(exam, questions);

            var formFile = new FormFile(fileStream, 0, fileStream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };

            return await _minioService.UploadAsync(
                formFile,
                "exams/random",
                new[] { ".xlsx" },
                20 * 1024 * 1024);
        }

        private static MemoryStream BuildGeneratedExamWorkbookStream(Exam exam, List<Question> questions)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("DeThiRandom");

            ws.Cell(1, 1).Value = "Title";
            ws.Cell(1, 2).Value = exam.Title;
            ws.Cell(2, 1).Value = "DurationMinutes";
            ws.Cell(2, 2).Value = exam.DurationMinutes;
            ws.Cell(3, 1).Value = "ExamType";
            ws.Cell(3, 2).Value = exam.ExamType;

            ws.Cell(5, 1).Value = "Content";
            ws.Cell(5, 2).Value = "QuestionType";
            ws.Cell(5, 3).Value = "OptionA";
            ws.Cell(5, 4).Value = "OptionB";
            ws.Cell(5, 5).Value = "OptionC";
            ws.Cell(5, 6).Value = "OptionD";
            ws.Cell(5, 7).Value = "CorrectAnswer";
            ws.Cell(5, 8).Value = "Level";
            ws.Cell(5, 9).Value = "Chapter";
            ws.Cell(5, 10).Value = "Explaination";
            ws.Cell(5, 11).Value = "Score";

            var row = 6;
            var defaultQuestionScore = questions.Any()
                ? Math.Round(10.0 / questions.Count, 4)
                : 0;

            foreach (var question in questions)
            {
                ws.Cell(row, 1).Value = question.Content;
                ws.Cell(row, 2).Value = question.QuestionType;
                ws.Cell(row, 3).Value = question.OptionA;
                ws.Cell(row, 4).Value = question.OptionB;
                ws.Cell(row, 5).Value = question.OptionC;
                ws.Cell(row, 6).Value = question.OptionD;
                ws.Cell(row, 7).Value = question.CorrectAnswer;
                ws.Cell(row, 8).Value = question.Level;
                ws.Cell(row, 9).Value = question.Chapter;
                ws.Cell(row, 10).Value = question.Explaination;
                ws.Cell(row, 11).Value = defaultQuestionScore;
                row++;
            }

            ws.Range(1, 1, 3, 2).Style.Font.Bold = true;
            ws.Row(5).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream;
        }

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = new string(value
                .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
                .ToArray());

            return string.IsNullOrWhiteSpace(cleaned) ? "DeThi" : cleaned.Trim();
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
        [Route("~/Teacher/Exams/UpdateQuestionScore")]
        public async Task<IActionResult> UpdateQuestionScore(int examQuestionId, double score)
        {
            var teacherId = GetTeacherId();
            if (teacherId == null) return Redirect("/Account/Login");

            var examQuestion = await _context.ExamQuestions
                .Include(eq => eq.Exam)
                .FirstOrDefaultAsync(eq => eq.ExamQuestionId == examQuestionId &&
                                           eq.Exam.TeacherId == teacherId.Value);

            if (examQuestion == null) return NotFound();

            if (examQuestion.Exam.IsPublished)
            {
                TempData["Error"] = "Vui lòng đóng đề thi trước khi chỉnh điểm câu hỏi.";
                return Redirect($"/Teacher/Exams/ManageQuestions/{examQuestion.ExamId}");
            }

            if (score <= 0)
            {
                TempData["Error"] = "Điểm câu hỏi phải lớn hơn 0.";
                return Redirect($"/Teacher/Exams/ManageQuestions/{examQuestion.ExamId}");
            }

            examQuestion.Score = Math.Round(score, 2);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật điểm câu hỏi.";
            return Redirect($"/Teacher/Exams/ManageQuestions/{examQuestion.ExamId}");
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
                    QuestionOrder = ++maxOrder,
                    Score = Math.Round(10.0 / (exam.ExamQuestions.Count + questions.Count), 4)
                });
            }

            exam.TotalQuestions = exam.ExamQuestions.Count + questions.Count;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm ngẫu nhiên {questions.Count} câu hỏi.";
            return Redirect($"/Teacher/Exams/ManageQuestions/{examId}");
        }
    }
}
