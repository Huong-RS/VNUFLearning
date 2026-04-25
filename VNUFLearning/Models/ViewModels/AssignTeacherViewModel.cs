using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VNUFLearning.Models.ViewModels
{
    public class AssignTeacherViewModel
    {
        public int SubjectId { get; set; }

        public string SubjectName { get; set; } = string.Empty;

        [Display(Name = "Giảng viên được phân công")]
        public List<int> SelectedTeacherIds { get; set; } = new();

        public List<User> AvailableTeachers { get; set; } = new();

        public List<User> AssignedTeachers { get; set; } = new();
    }
}