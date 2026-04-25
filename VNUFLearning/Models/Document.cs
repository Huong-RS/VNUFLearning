using VNUFLearning.Models;
namespace VNUFLearning.Models;
public partial class Document
{
    public int DocumentId { get; set; }

    public string Title { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public string? FileName { get; set; }
   

    public string? FileType { get; set; }

    public long? FileSize { get; set; }

    public int DownloadCount { get; set; }

    public bool IsActive { get; set; }
  

    public int SubjectId { get; set; }

    public int UploadedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Subject Subject { get; set; } = null!;
    public virtual User UploadedByNavigation { get; set; } = null!;
}