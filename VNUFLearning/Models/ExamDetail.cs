using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class ExamDetail
{
    public int DetailId { get; set; }

    public int ExamResultId { get; set; }

    public int QuestionId { get; set; }

    public string? UserAnswer { get; set; }

    public bool? IsCorrect { get; set; }

    public virtual ExamResult ExamResult { get; set; } = null!;
    public double? EssayScore { get; set; }
    public double? SimilarityPercent { get; set; }
    public string? AiFeedback { get; set; }
    public virtual Question Question { get; set; } = null!;
}
