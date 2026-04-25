using System;

namespace VNUFLearning.Models
{
    public partial class SubjectAssignment
    {
        public int AssignmentId { get; set; }

        public int SubjectId { get; set; }

        public int TeacherId { get; set; }

        public DateTime AssignedAt { get; set; }

        public virtual Subject Subject { get; set; } = null!;

        public virtual User Teacher { get; set; } = null!;
    }
}