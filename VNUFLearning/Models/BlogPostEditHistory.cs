namespace VNUFLearning.Models
{
    public partial class BlogPostEditHistory
    {
        public int HistoryId { get; set; }

        public int PostId { get; set; }

        public string? OldTitle { get; set; }

        public string? OldContent { get; set; }

        public string? OldImageUrl { get; set; }

        public string? OldAttachmentUrl { get; set; }

        public int EditedByUserId { get; set; }

        public DateTime EditedAt { get; set; }

        public virtual BlogPost Post { get; set; } = null!;

        public virtual User EditedByUser { get; set; } = null!;
    }
}