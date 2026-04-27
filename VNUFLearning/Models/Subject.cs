using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class Subject
{
    public int SubjectId { get; set; }

    public string SubjectName { get; set; } = null!;

    public string? SubjectCode { get; set; }

    public string? Description { get; set; }

    public int? Credits { get; set; }

    public string? DepartmentName { get; set; }

    public string? CoverImageUrl { get; set; }


    public bool IsActive { get; set; }
    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();

    public virtual ICollection<SubjectAssignment> SubjectAssignments { get; set; } = new List<SubjectAssignment>();
}