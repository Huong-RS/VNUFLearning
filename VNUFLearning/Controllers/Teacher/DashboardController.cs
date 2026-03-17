using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]

    [Route("Teacher/[controller]/[action]")]
    public class DashboardController : Controller
    {
        [Route("~/Teacher/Dashboard")]
        [Route("")]
        public IActionResult Index()
        {
            return View("~/Views/teacher/Dashboard/Index.cshtml");
        }
    }
}