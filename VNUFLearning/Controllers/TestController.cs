using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers
{
    public class TestController : Controller
    {
        public IActionResult Index()
        {
            return Content("App dang chay binh thuong");
        }
    }
}