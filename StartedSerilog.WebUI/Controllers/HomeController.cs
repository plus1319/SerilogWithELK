using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using StartedSerilog.Core.Attributes;
using StartedSerilog.WebUI.Models;

namespace StartedSerilog.WebUI.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        [AllowAnonymous]
        [LogUsage("index is fired.")]
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            Log.Information("we got about ...");
            ViewData["Message"] = "Your application description page.";

            return View();
        }
        
        public IActionResult BadPage()
        {
            ViewData["Message"] = "Your exception page.";
            throw new Exception("Bad Page fired");
            return View();
        }
        public IActionResult BadPageWithQuery()
        {
            throw new Exception("bad page query fired");
        }
        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        [LogUsage("Error page is fired.")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
