using System.Diagnostics;
using AirbnbBooking.Models;
using Microsoft.AspNetCore.Mvc;

namespace AirbnbBooking.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => RedirectToAction("Catalog", "Property");

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
