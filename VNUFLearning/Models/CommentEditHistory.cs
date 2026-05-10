namespace VNUFLearning.Models
{
    public partial class CommentEditHistory
    {
        public int HistoryId { get; set; }

        public int CommentId { get; set; }

        public string? OldContent { get; set; }

        public string? OldImageUrl { get; set; }

        public string? OldAttachmentUrl { get; set; }

        public int EditedByUserId { get; set; }

        public DateTime EditedAt { get; set; }

        public virtual Comment Comment { get; set; } = null!;

        public virtual User EditedByUser { get; set; } = null!;
    }
}