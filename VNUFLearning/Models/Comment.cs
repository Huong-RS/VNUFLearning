using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class Comment
{
    public int CommentId { get; set; }

    public int PostId { get; set; }
    public bool IsEdited { get; set; }

    public DateTime? LastEditedAt { get; set; }

    public virtual ICollection<CommentEditHistory> EditHistories { get; set; } = new List<CommentEditHistory>();
    public int UserId { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageObjectName { get; set; }

    public string? AttachmentObjectName { get; set; }
    public string? AttachmentUrl { get; set; }

    public string? AttachmentName { get; set; }

    public string? AttachmentType { get; set; }

    public long? AttachmentSize { get; set; }
    public string Content { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual BlogPost Post { get; set; } = null!;
    public int? ParentCommentId { get; set; }

    public virtual Comment? ParentComment { get; set; }

    public virtual ICollection<Comment> Replies { get; set; }
        = new List<Comment>();
    public virtual User User { get; set; } = null!;
}
