using System;
using System.Collections.Generic;

namespace VNUFLearning.Models.ViewModels
{
    public class UserProfileViewModel
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string Role { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public DateTime? JoinDate { get; set; }

        public string Code { get; set; } = string.Empty;

        public string? Bio { get; set; }

        public List<UserProfileActivityViewModel> RecentActivities { get; set; } = new();
    }

    public class UserProfileActivityViewModel
    {
        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? CreatedAt { get; set; }

        public string IconCssClass { get; set; } = "fa-solid fa-circle";

        public string BadgeCssClass { get; set; } = "bg-secondary";
    }
}
