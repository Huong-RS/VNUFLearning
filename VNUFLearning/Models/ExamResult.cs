using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class ExamResult
{
    public int ExamResultId { get; set; }

    public int UserId { get; set; }

    public int SubjectId { get; set; }

    public double? Score { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public string? Status { get; set; }

    public string? AiFeedback { get; set; }

    public virtual ICollection<ExamDetail> ExamDetails { get; set; } = new List<ExamDetail>();

    public virtual Subject Subject { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
