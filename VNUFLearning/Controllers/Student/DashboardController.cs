using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Student
{
    [Authorize(Roles = "Student")]
    public class DashboardController : Controller
    {
       
        public IActionResult Index()
        {
            return View();
        }
    }
}
