using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Student
{
    public class PracticeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
