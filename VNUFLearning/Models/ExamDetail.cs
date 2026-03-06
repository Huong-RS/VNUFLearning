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

    public virtual Question Question { get; set; } = null!;
}
