namespace VNUFLearning.Models.ViewModels
{
    public class TeacherDashboardViewModel
    {
        public string TeacherName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public int TotalCourses { get; set; }

        public int TotalStudents { get; set; }

        public int PendingExamsToGrade { get; set; }

        public int PendingBlogsToApprove { get; set; }

        public List<TodoItemViewModel> TodoItems { get; set; } = new();

        public List<CourseViewModel> ActiveCourses { get; set; } = new();

        public List<ActivityLogViewModel> RecentActivities { get; set; } = new();
    }

    public class TodoItemViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Url { get; set; } = "#";

        public int Count { get; set; }

        public string IconCssClass { get; set; } = "fa-solid fa-circle-info";
    }

    public class CourseViewModel
    {
        public int SubjectId { get; set; }

        public string CourseCode { get; set; } = string.Empty;

        public string CourseName { get; set; } = string.Empty;

        public int StudentCount { get; set; }

        public int ExamCount { get; set; }

        public int DocumentCount { get; set; }
    }

    public class ActivityLogViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime? CreatedAt { get; set; }

        public string ActorName { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string IconCssClass { get; set; } = "fa-solid fa-clock";
    }
}
