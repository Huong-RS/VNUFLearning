using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Admin
{
    public class StatisticsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
