using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Admin
{
    public class DocumentsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
