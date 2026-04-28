using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VNUFLearning.Data;
using VNUFLearning.Services;
using VNUFLearning.Models;

namespace VNUFLearning.Controllers.Student
{
    [Route("Student/[controller]")]
    public class PracticeController : Controller
    {
        private readonly VnufLearningContext _context;
        private readonly GeminiService _geminiService;

        public PracticeController(VnufLearningContext context, GeminiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
        }
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var vm = new PracticeHomeViewModel
            {
                Subjects = await _context.Subjects
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SubjectName)
                    .Select(s => new SelectListItem
                    {
                        Value = s.SubjectId.ToString(),
                        Text = s.SubjectName
                    })
                    .ToListAsync(),

                MockExams = await _context.Exams
                    .Include(e => e.Subject)
                    .Where(e => e.IsPublished)
                    .OrderByDescending(e => e.CreatedAt)
                    .Select(e => new MockExamCardViewModel
                    {
                        ExamId = e.ExamId,
                        Title = e.Title,
                        SubjectName = e.Subject.SubjectName,
                        DurationMinutes = e.DurationMinutes,
                        TotalQuestions = e.TotalQuestions,
                        Description = e.Description
                    })
                    .ToListAsync()
            };

            return View("~/Views/Student/Practice/Index.cshtml", vm);
        }

        [HttpPost("Start")]
        public async Task<IActionResult> Start(PracticeStartInput input)
        {
            if (input.SubjectId <= 0)
            {
                TempData["Error"] = "Vui lòng chọn học phần.";
                return RedirectToAction(nameof(Index));
            }

            if (input.QuestionCount <= 0)
                input.QuestionCount = 2;

            IQueryable<Question> query = _context.Questions
                .Where(q => q.IsActive && q.SubjectId == input.SubjectId);

            if (input.SmartMode)
            {
                var userId = GetCurrentUserId();

                var wrongIds = await _context.ExamDetails
                    .Include(d => d.ExamResult)
                    .Where(d => d.ExamResult.UserId == userId
                                && d.ExamResult.SubjectId == input.SubjectId
                                && d.IsCorrect == false)
                    .Select(d => d.QuestionId)
                    .Distinct()
                    .ToListAsync();

                query = query.Where(q => wrongIds.Contains(q.QuestionId));
            }

            if (input.StructureType == "mcq")
                query = query.Where(q => q.QuestionType == 1);
            else if (input.StructureType == "essay")
                query = query.Where(q => q.QuestionType == 2);

            var questions = await query
                .OrderBy(q => Guid.NewGuid())
                .Take(input.QuestionCount)
                .Select(q => new DoQuestionViewModel
                {
                    QuestionId = q.QuestionId,
                    Content = q.Content,
                    ImageUrl = q.ImageUrl,
                    QuestionType = q.QuestionType,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectAnswer = q.CorrectAnswer,
                    Explaination = q.Explaination
                })
                .ToListAsync();

            if (!questions.Any())
            {
                TempData["Error"] = "Không có câu hỏi phù hợp với lựa chọn.";
                return RedirectToAction(nameof(Index));
            }

            var subjectName = await _context.Subjects
                .Where(s => s.SubjectId == input.SubjectId)
                .Select(s => s.SubjectName)
                .FirstOrDefaultAsync();

            var vm = new DoPracticeViewModel
            {
                Mode = "practice",
                SubjectId = input.SubjectId,
                SubjectName = subjectName ?? "Luyện tập",
                StartedAt = DateTime.Now,
                DurationMinutes = 0,
                Questions = questions
            };

            return View("~/Views/Student/Practice/DoPractice.cshtml", vm);
        }

        [HttpGet("Mock/{id}")]
        public async Task<IActionResult> Mock(int id)
        {
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .Include(e => e.ExamQuestions)
                    .ThenInclude(eq => eq.Question)
                .FirstOrDefaultAsync(e => e.ExamId == id && e.IsPublished);

            if (exam == null)
                return NotFound();

            var questions = exam.ExamQuestions
                .OrderBy(eq => eq.QuestionOrder)
                .Select(eq => eq.Question)
                .Where(q => q.IsActive)
                .Select(q => new DoQuestionViewModel
                {
                    QuestionId = q.QuestionId,
                    Content = q.Content,
                    ImageUrl = q.ImageUrl,
                    QuestionType = q.QuestionType,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectAnswer = null,
                    Explaination = null
                })
                .ToList();

            var vm = new DoPracticeViewModel
            {
                Mode = "mock",
                ExamId = exam.ExamId,
                SubjectId = exam.SubjectId,
                SubjectName = exam.Subject.SubjectName,
                StartedAt = DateTime.Now,
                DurationMinutes = exam.DurationMinutes,
                Questions = questions
            };

            return View("~/Views/Student/Practice/DoPractice.cshtml", vm);
        }

        [HttpPost("Submit")]
        public async Task<IActionResult> Submit(SubmitPracticeInput input)
        {
            var userId = GetCurrentUserId();

            var questionIds = input.Answers.Select(a => a.QuestionId).ToList();

            var questions = await _context.Questions
                .Where(q => questionIds.Contains(q.QuestionId))
                .ToListAsync();

            int totalMcq = 0;
            int correctMcq = 0;
            bool hasEssay = false;

            var result = new ExamResult
            {
                UserId = userId,
                SubjectId = input.SubjectId,
                ExamId = input.ExamId,
                StartedAt = input.StartedAt,
                FinishedAt = DateTime.Now,
                Status = "Completed"
            };

            foreach (var answer in input.Answers)
            {
                var question = questions.FirstOrDefault(q => q.QuestionId == answer.QuestionId);
                if (question == null) continue;

                bool? isCorrect = null;
                double? essayScore = null;
                double? similarityPercent = null;
                string? aiFeedback = null;

                // TRẮC NGHIỆM
                if (question.QuestionType == 1)
                {
                    totalMcq++;

                    isCorrect = string.Equals(
                        answer.UserAnswer?.Trim(),
                        question.CorrectAnswer?.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    );

                    if (isCorrect == true)
                        correctMcq++;
                }

                // TỰ LUẬN → GEMINI CHẤM
                else
                {
                    hasEssay = true;

                    var studentAnswer = answer.UserAnswer ?? "";

                    if (!string.IsNullOrWhiteSpace(studentAnswer))
                    {
                        var aiResult = await _geminiService.GradeEssayAsync(
                            question.Content,
                            question.CorrectAnswer ?? "",
                            studentAnswer
                        );

                        essayScore = Math.Round(aiResult.Score, 2);
                        similarityPercent = Math.Round(aiResult.Percent, 2);
                        if (aiResult.IsError)
                        {
                            essayScore = null;
                            similarityPercent = null;
                            aiFeedback =
                                "Gemini AI hiện chưa chấm được bài này.\n" +
                                aiResult.Comment + "\n\n" +
                                aiResult.Advice;
                        }
                        else
                        {
                            essayScore = Math.Round(aiResult.Score, 2);
                            similarityPercent = Math.Round(aiResult.Percent, 2);

                            aiFeedback =
                                $"Mức độ đúng: {similarityPercent}%\n" +
                                $"Điểm tự luận: {essayScore}/10\n\n" +
                                $"Nhận xét: {aiResult.Comment}\n\n" +
                                $"Góp ý: {aiResult.Advice}";
                        }
                        aiFeedback =
                            $"Mức độ đúng: {similarityPercent}%\n" +
                            $"Điểm tự luận: {essayScore}/10\n\n" +
                            $"Nhận xét: {aiResult.Comment}\n\n" +
                            $"Góp ý: {aiResult.Advice}";
                    }
                    else
                    {
                        essayScore = 0;
                        similarityPercent = 0;
                        aiFeedback = "Sinh viên chưa nhập câu trả lời tự luận.";
                    }
                }

                result.ExamDetails.Add(new ExamDetail
                {
                    QuestionId = question.QuestionId,
                    UserAnswer = answer.UserAnswer,
                    IsCorrect = isCorrect,
                    EssayScore = essayScore,
                    SimilarityPercent = similarityPercent,
                    AiFeedback = aiFeedback
                });
            }

            // TÍNH ĐIỂM
            var essayDetails = result.ExamDetails
                .Where(x => x.EssayScore.HasValue)
                .ToList();

            double? mcqScore = totalMcq > 0
                ? Math.Round((double)correctMcq / totalMcq * 10, 2)
                : null;

            double? essayScoreAvg = essayDetails.Any()
                ? Math.Round(essayDetails.Average(x => x.EssayScore!.Value), 2)
                : null;

            if (mcqScore.HasValue && essayScoreAvg.HasValue)
            {
                result.Score = Math.Round(
                    (mcqScore.Value + essayScoreAvg.Value) / 2,
                    2
                );
            }
            else if (mcqScore.HasValue)
            {
                result.Score = mcqScore.Value;
            }
            else if (essayScoreAvg.HasValue)
            {
                result.Score = essayScoreAvg.Value;
            }
            else
            {
                result.Score = 0;
            }

            if (hasEssay)
            {
                result.AiFeedback = "Gemini AI đã phân tích phần tự luận.";
            }

            // LƯU DB
            _context.ExamResults.Add(result);
            await _context.SaveChangesAsync();

            return RedirectToAction("Success", new { id = result.ExamResultId });
        }
        [HttpGet("Success/{id}")]
        public async Task<IActionResult> Success(int id)
        {
            var result = await _context.ExamResults
                .Include(r => r.Subject)
                .FirstOrDefaultAsync(r => r.ExamResultId == id);

            if (result == null)
                return NotFound();

            return View("~/Views/Student/Practice/Success.cshtml", result);
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("UserId");

            return int.TryParse(value, out int id) ? id : 1;
        }
    }

    public class PracticeHomeViewModel
    {
        public List<SelectListItem> Subjects { get; set; } = new();
        public List<MockExamCardViewModel> MockExams { get; set; } = new();
    }

    public class MockExamCardViewModel
    {
        public int ExamId { get; set; }
        public string Title { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public int DurationMinutes { get; set; }
        public int TotalQuestions { get; set; }
        public string? Description { get; set; }
    }

    public class PracticeStartInput
    {
        public int SubjectId { get; set; }
        public string StructureType { get; set; } = "mixed";
        public int QuestionCount { get; set; } = 2;
        public bool SmartMode { get; set; }
    }

    public class DoPracticeViewModel
    {
        public string Mode { get; set; } = "practice";
        public int? ExamId { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = "";
        public DateTime StartedAt { get; set; }
        public int DurationMinutes { get; set; }
        public List<DoQuestionViewModel> Questions { get; set; } = new();
    }

    public class DoQuestionViewModel
    {
        public int QuestionId { get; set; }
        public string Content { get; set; } = "";
        public string? ImageUrl { get; set; }
        public int QuestionType { get; set; }
        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? Explaination { get; set; }
    }

    public class SubmitPracticeInput
    {
        public string Mode { get; set; } = "practice";
        public int? ExamId { get; set; }
        public int SubjectId { get; set; }
        public DateTime StartedAt { get; set; }
        public List<SubmitAnswerInput> Answers { get; set; } = new();
    }

    public class SubmitAnswerInput
    {
        public int QuestionId { get; set; }
        public string? UserAnswer { get; set; }
    }
}