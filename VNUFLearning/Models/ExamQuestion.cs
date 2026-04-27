namespace VNUFLearning.Models;

public partial class ExamQuestion
{
    public int ExamQuestionId { get; set; }

    public int ExamId { get; set; }

    public int QuestionId { get; set; }

    public int? QuestionOrder { get; set; }

    public virtual Exam Exam { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;
}