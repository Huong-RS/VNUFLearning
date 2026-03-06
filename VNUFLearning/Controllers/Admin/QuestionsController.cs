using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Admin
{
    public class QuestionsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
