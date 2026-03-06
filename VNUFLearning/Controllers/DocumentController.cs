using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers
{
    public class DocumentController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
