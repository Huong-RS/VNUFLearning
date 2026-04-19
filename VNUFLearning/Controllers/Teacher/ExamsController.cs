using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using VNUFLearning.Data;

namespace VNUFLearning.Controllers
{
    // Cấp quyền cho Giảng viên (và cả Admin nếu cần)
    [Authorize(Roles = "GiangVien,Admin")]
    [Route("Teacher/[controller]/[action]")]
    public class ExamsController : Controller
    {
        private readonly VnufLearningContext _context;

        public ExamsController(VnufLearningContext context)
        {
            _context = context;
        }

        // Chỉ hiển thị giao diện, tạm thời chưa lưu Database
        [HttpGet]
        // Xử lý khi Giảng viên bấm nút "Bắt đầu Sinh đề"
        [HttpPost]
        [Route("~/Teacher/Exams/Create")]
        public IActionResult Create(string examName, int subjectId, int duration, int numberOfQuestions)
        {
            // Vì CSDL của bạn hiện tại chưa có bảng Exam (Đề thi) để lưu trữ vĩnh viễn
            // Nên tạm thời chúng ta sẽ trả về thông báo thành công "ảo" để Giảng viên xem giao diện.
            // Nếu muốn lưu thật, bạn sẽ cần thiết kế thêm bảng Exam vào SQL Server sau nhé.

            TempData["Success"] = $"Mô phỏng thành công: Đã tạo đề '{examName}' với {numberOfQuestions} câu hỏi. (Cần cập nhật CSDL để lưu trữ chính thức).";

            // Tải lại trang Create để hiển thị thông báo
            return RedirectToAction("Create");
        }
    }
}