using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class BlogPost
{
    public int PostId { get; set; }
    public string? AttachmentUrl { get; set; }
   
    public string? AttachmentName { get; set; }

    public string? AttachmentType { get; set; }

    public long? AttachmentSize { get; set; }

    public int LikeCount { get; set; }
    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int AuthorId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public string? ImageUrl { get; set; }
    public int? ViewCount { get; set; }
    public bool IsPublished { get; set; }
    public virtual User Author { get; set; } = null!;
    public virtual ICollection<BlogLike> BlogLikes { get; set; } = new List<BlogLike>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
