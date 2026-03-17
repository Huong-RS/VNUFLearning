using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Admin
{
    // Bắt buộc người dùng phải có quyền Admin mới được vào
    [Authorize(Roles = "Admin")]

    // Ép đường dẫn phải bắt đầu bằng /Admin/Dashboard/...
    [Route("Admin/[controller]/[action]")]
    public class DashboardController : Controller
    {
        // Thiết lập hàm Index làm trang mặc định khi truy cập /Admin/Dashboard
        [Route("~/Admin/Dashboard")]
        [Route("")]
        public IActionResult Index()
        {
            // Trỏ đường dẫn tuyệt đối đến View để tránh lỗi hệ thống tìm sai chỗ
            return View("~/Views/admin/Dashboard/Index.cshtml");
        }
    }
}