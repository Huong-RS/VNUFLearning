using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers
{
    public class BlogModerationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
