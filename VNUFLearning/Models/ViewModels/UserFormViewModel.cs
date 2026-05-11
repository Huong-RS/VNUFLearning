using System.ComponentModel.DataAnnotations;

namespace VNUFLearning.Models.ViewModels
{
    public class UserFormViewModel
    {
        public string? ClassName { get; set; }

        public string? DepartmentName { get; set; }
        public int UserId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã tài khoản.")]
        [Display(Name = "Mã tài khoản")]
        public string StudentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [MaxLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        [Display(Name = "Vai trò")]
        public int RoleId { get; set; }

        [Display(Name = "Mật khẩu")]
        public string? Password { get; set; }
    }
}
