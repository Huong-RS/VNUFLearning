using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class BlogPost
{
    public int PostId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int AuthorId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? ViewCount { get; set; }

    public virtual User Author { get; set; } = null!;

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
