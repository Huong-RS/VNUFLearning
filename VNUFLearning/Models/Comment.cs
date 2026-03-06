using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class Comment
{
    public int CommentId { get; set; }

    public int PostId { get; set; }

    public int UserId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual BlogPost Post { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
