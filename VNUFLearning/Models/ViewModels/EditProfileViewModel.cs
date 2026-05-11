using System.ComponentModel.DataAnnotations;

namespace VNUFLearning.Models.ViewModels;

public class EditProfileViewModel
{
    [Phone(ErrorMessage = "So dien thoai khong hop le.")]
    [MaxLength(20, ErrorMessage = "So dien thoai khong duoc vuot qua 20 ky tu.")]
    public string? Phone { get; set; }

    [MaxLength(500, ErrorMessage = "Gioi thieu khong duoc vuot qua 500 ky tu.")]
    public string? Bio { get; set; }
}
