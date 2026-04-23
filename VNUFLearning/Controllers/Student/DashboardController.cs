using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Student
{
    [Authorize(Roles = "SinhVien")]
    [Route("Student/[controller]/[action]")]
    public class DashboardController : Controller
    {
        [HttpGet]
        [Route("~/Student")]
        [Route("~/Student/Dashboard")]
        [Route("~/Student/Dashboard/Index")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Dashboard Sinh viên";
            return View("~/Views/Student/Dashboard/Index.cshtml");
        }
    }
}