using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class Subject
{
    public int SubjectId { get; set; }

    public string SubjectName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}
