using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Student
{
    [Authorize(Roles = "SinhVien")]

    [Route("Student/[controller]/[action]")]
    public class DashboardController : Controller
    {
        [Route("~/Student/Dashboard")]
        [Route("")]
        public IActionResult Index()
        {
            return View("~/Views/student/Dashboard/Index.cshtml");
        }
    }
}