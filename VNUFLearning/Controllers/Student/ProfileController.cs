using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Student
{
    public class ProfileController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
