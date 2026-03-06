using Microsoft.AspNetCore.Mvc;

namespace VNUFLearning.Controllers.Admin
{
    public class UsersController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
