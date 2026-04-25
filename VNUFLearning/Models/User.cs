using System;
using System.Collections.Generic;

namespace VNUFLearning.Models;

public partial class User
{
    public int UserId { get; set; }
    public string? ClassName { get; set; }

    public string? DepartmentName { get; set; }
    public string StudentCode { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string? AvatarUrl { get; set; }

    public int RoleId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();

    public virtual ICollection<SubjectAssignment> SubjectAssignments { get; set; } = new List<SubjectAssignment>();

    public virtual Role Role { get; set; } = null!;
}