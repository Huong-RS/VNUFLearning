using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class DashboardController : Controller
    {
        [HttpGet]
        [Route("~/Admin")]
        [Route("~/Admin/Dashboard")]
        [Route("~/Admin/Dashboard/Index")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Dashboard Admin";
            return View("~/Views/Admin/Dashboard/Index.cshtml");
        }
    }
}