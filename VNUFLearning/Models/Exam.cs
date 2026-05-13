namespace VNUFLearning.Models;

public partial class Exam
{
    public int ExamId { get; set; }

    public string Title { get; set; } = null!;

    public int SubjectId { get; set; }

    public int TeacherId { get; set; }

    public int DurationMinutes { get; set; }

    public int TotalQuestions { get; set; }
    public int ExamType { get; set; }

    public int? Level { get; set; }

    public string? ChapterFrom { get; set; }

    public string? ChapterTo { get; set; }
    public virtual ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
    public string? Description { get; set; }
    public bool IsPublished { get; set; }

    public string? SourceFileUrl { get; set; }

    public string? SourceFileObjectName { get; set; }

    public string? SourceFileName { get; set; }

    public string? SourceFileType { get; set; }

    public long? SourceFileSize { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Subject Subject { get; set; } = null!;

    public virtual User Teacher { get; set; } = null!;

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
}
