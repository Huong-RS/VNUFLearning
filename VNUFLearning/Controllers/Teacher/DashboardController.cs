using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Teacher
{
    [Authorize(Roles = "GiangVien")]
    [Route("Teacher/[controller]/[action]")]
    public class DashboardController : Controller
    {
        [HttpGet]
        [Route("~/Teacher")]
        [Route("~/Teacher/Dashboard")]
        [Route("~/Teacher/Dashboard/Index")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Dashboard Giảng viên";
            return View("~/Views/Teacher/Dashboard/Index.cshtml");
        }
    }
}