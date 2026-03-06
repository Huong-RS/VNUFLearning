using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers
{
    public class BlogController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
