using System.ComponentModel.DataAnnotations;

namespace VNUFLearning.Models.ViewModels
{
    public class UserFormViewModel
    {
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

        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        [Display(Name = "Vai trò")]
        public int RoleId { get; set; }

        [Display(Name = "Mật khẩu")]
        public string? Password { get; set; }
    }
}