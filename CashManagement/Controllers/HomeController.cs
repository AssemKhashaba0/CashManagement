using CashManagement.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CashManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (!User.Identity.IsAuthenticated)
            {
                // Redirect to Login or custom page
                return Redirect("/Identity/Account/Login"); // أو أي صفحة تانية انت عايزها
            }

            return View(); // لو مسجل دخوله يعرض الصفحة عادي
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
