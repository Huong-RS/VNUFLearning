namespace VNUFLearning.Models;

public partial class BlogLike
{
    public int LikeId { get; set; }

    public int PostId { get; set; }

    public int UserId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual BlogPost Post { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}