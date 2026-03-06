using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class Question
{
    

    public int QuestionId { get; set; }

    public string Content { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public int QuestionType { get; set; }

    public string? OptionA { get; set; }

    public string? OptionB { get; set; }

    public string? OptionC { get; set; }

    public string? OptionD { get; set; }

    public string? CorrectAnswer { get; set; }

    public string? Explaination { get; set; }

    public int SubjectId { get; set; }

    public int? Level { get; set; }

    public string? Chapter { get; set; } // Ví dụ: "Chương 1", "Đề thi giữa kỳ 2025"

    public virtual ICollection<ExamDetail> ExamDetails { get; set; } = new List<ExamDetail>();

    public virtual Subject Subject { get; set; } = null!;
}
