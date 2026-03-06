using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Admin
{
    public class SubjectsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
