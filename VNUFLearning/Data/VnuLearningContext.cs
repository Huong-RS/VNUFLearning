using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using VNUFLearning.Models;

namespace VNUFLearning.Data;

public partial class VnufLearningContext : DbContext
{
    public VnufLearningContext()
    {
    }
    public virtual DbSet<SubjectAssignment> SubjectAssignments { get; set; }
    public VnufLearningContext(DbContextOptions<VnufLearningContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BlogPost> BlogPosts { get; set; }

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<ExamDetail> ExamDetails { get; set; }

    public virtual DbSet<ExamResult> ExamResults { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    public virtual DbSet<User> Users { get; set; }

   
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Exam>(entity =>
        {
            entity.Property(e => e.ExamType).HasDefaultValue(1);

            entity.Property(e => e.ChapterFrom).HasMaxLength(100);

            entity.Property(e => e.ChapterTo).HasMaxLength(100);

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasKey(e => e.ExamId);

            entity.Property(e => e.Title).HasMaxLength(200);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.Property(e => e.DurationMinutes).HasDefaultValue(45);

            entity.Property(e => e.TotalQuestions).HasDefaultValue(20);

            entity.Property(e => e.IsPublished).HasDefaultValue(false);

            entity.HasOne(e => e.Subject)
                .WithMany(s => s.Exams)
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exams_Subject");

            entity.HasOne(e => e.Teacher)
                .WithMany(u => u.Exams)
                .HasForeignKey(e => e.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exams_Teacher");
        });

        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.HasKey(e => e.ExamQuestionId);

            entity.HasOne(e => e.Exam)
                .WithMany(e => e.ExamQuestions)
                .HasForeignKey(e => e.ExamId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ExamQuestions_Exam");

            entity.HasOne(e => e.Question)
                .WithMany(q => q.ExamQuestions)
                .HasForeignKey(e => e.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ExamQuestions_Question");
        });
        modelBuilder.Entity<SubjectAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId);

            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Subject)
                .WithMany(p => p.SubjectAssignments)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SubjectAssignments_Subjects");

            entity.HasOne(d => d.Teacher)
                .WithMany(p => p.SubjectAssignments)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SubjectAssignments_Users");
        });
        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("PK__BlogPost__AA126018EE206CC6");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.ViewCount).HasDefaultValue(0);

            entity.HasOne(d => d.Author).WithMany(p => p.BlogPosts)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BlogPosts__Autho__5535A963");
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__Comments__C3B4DFCADF1E1B0A");

            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Post).WithMany(p => p.Comments)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Comments__PostId__59063A47");

            entity.HasOne(d => d.User).WithMany(p => p.Comments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Comments__UserId__59FA5E80");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.DocumentId).HasName("PK__Document__1ABEEF0FC2765EB6");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Subject).WithMany(p => p.Documents)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Documents__Subje__412EB0B6");
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.FileType).HasMaxLength(50);
            entity.Property(e => e.FileSize);
            entity.Property(e => e.DownloadCount).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.Documents)
                .HasForeignKey(d => d.UploadedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Documents__Uploa__4222D4EF");
        });

        modelBuilder.Entity<ExamDetail>(entity =>
        {
            entity.HasKey(e => e.DetailId).HasName("PK__ExamDeta__135C316D12C453B5");

            entity.HasOne(d => d.ExamResult).WithMany(p => p.ExamDetails)
                .HasForeignKey(d => d.ExamResultId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExamDetai__ExamR__4F7CD00D");

            entity.HasOne(d => d.Question).WithMany(p => p.ExamDetails)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExamDetai__Quest__5070F446");
        });

        modelBuilder.Entity<ExamResult>(entity =>
        {
            entity.HasOne(d => d.Exam)
    .WithMany(p => p.ExamResults)
    .HasForeignKey(d => d.ExamId)
    .HasConstraintName("FK_ExamResults_Exams");
            entity.HasKey(e => e.ExamResultId).HasName("PK__ExamResu__3DBFDE2673C5DCBF");

            entity.Property(e => e.FinishedAt).HasColumnType("datetime");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Completed");

            entity.HasOne(d => d.Subject).WithMany(p => p.ExamResults)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExamResul__Subje__4CA06362");

            entity.HasOne(d => d.User).WithMany(p => p.ExamResults)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExamResul__UserI__4BAC3F29");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("PK__Question__0DC06FAC0670DA65");

            entity.Property(e => e.CorrectAnswer)
     .HasMaxLength(1000);

            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.Level).HasDefaultValue(1);
            entity.Property(e => e.QuestionType).HasDefaultValue(1);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Subject).WithMany(p => p.Questions)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Questions__Subje__46E78A0C");

            entity.HasOne(d => d.CreatedByUser).WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Questions_Users_CreatedBy");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1A14DB14E8");

            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.SubjectId).HasName("PK__Subjects__AC1BA3A842BECB86");

            entity.Property(e => e.SubjectName).HasMaxLength(100);

            entity.Property(e => e.SubjectCode).HasMaxLength(30);

            entity.Property(e => e.Description).HasMaxLength(500);

            entity.Property(e => e.DepartmentName).HasMaxLength(100);

            entity.Property(e => e.CoverImageUrl).HasMaxLength(500);

            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.ClassName).HasMaxLength(50);
            entity.Property(e => e.DepartmentName).HasMaxLength(100);
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CD6F29042");

            entity.HasIndex(e => e.StudentCode, "UQ__Users__1FC886047F5AAFA0").IsUnique();

            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.StudentCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__RoleId__3B75D760");
        });
        OnModelCreatingPartial(modelBuilder);
    }
    public virtual DbSet<Exam> Exams { get; set; }

    public virtual DbSet<ExamQuestion> ExamQuestions { get; set; }
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
