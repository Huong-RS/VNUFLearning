using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Data;
using VNUFLearning.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VNUFLearning.Controllers.Teacher
{
    public class ExamsController : Controller
    {
        private readonly VnufLearningContext _context;

        public ExamsController(VnufLearningContext context)
        {
            _context = context;
        }

        // 1. Trang chọn môn thi
        public IActionResult SelectSubject()
        {
            var subjects = _context.Subjects.ToList();
            return View(subjects);
        }

        // 2. Tạo đề thi theo môn
        public async Task<IActionResult> StartExam(int subjectId)
        {
            var subject = await _context.Subjects.FindAsync(subjectId);
            ViewBag.SubjectName = subject?.SubjectName ?? "Môn học";
            ViewBag.SubjectId = subjectId;

            // Lấy ngẫu nhiên 10 câu hỏi
            var questions = await _context.Questions
                .Where(q => q.SubjectId == subjectId && q.QuestionType == 1)
                .OrderBy(q => Guid.NewGuid())
                .Take(10)
                .ToListAsync();

            if (questions == null || !questions.Any())
            {
                return Content("Chưa có câu hỏi trắc nghiệm nào cho môn học này.");
            }

            return View(questions);
        }

        // 3. Nộp bài và chấm điểm (ĐÃ BỔ SUNG timeTaken)
        [HttpPost]
        public IActionResult SubmitExam(Dictionary<int, string> answers, int timeTaken)
        {
            int correct = 0;

            if (answers == null || !answers.Any())
            {
                answers = new Dictionary<int, string>();
            }

            var questionIds = answers.Keys.ToList();

            var questions = _context.Questions
                .Where(q => questionIds.Contains(q.QuestionId))
                .ToList();

            foreach (var q in questions)
            {
                if (answers.ContainsKey(q.QuestionId) && answers[q.QuestionId] == q.CorrectAnswer)
                {
                    correct++;
                }
            }

            int total = 10;
            double score = total > 0 ? Math.Round((double)correct / total * 10, 2) : 0;

            // Xử lý thời gian làm bài (đổi từ giây sang dạng mm:ss)
            TimeSpan time = TimeSpan.FromSeconds(timeTaken);
            ViewBag.TimeTaken = time.ToString(@"mm\:ss");

            ViewBag.Score = score;
            ViewBag.Correct = correct;
            ViewBag.Total = total;

            return View("Result");
        }
    }
}