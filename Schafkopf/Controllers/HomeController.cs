using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Schafkopf.Models;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace Schafkopf.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _env;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet("/tts/files")]
        public IActionResult GetTtsFiles()
        {
            var list = new List<object>();
            var ttsFolder = Path.Combine(_env.WebRootPath, "tts");
            if (Directory.Exists(ttsFolder))
            {
                var files = Directory.GetFiles(ttsFolder).OrderBy(f => f).ToList();
                foreach (var f in files)
                {
                    var name = Path.GetFileName(f);
                    var url = $"/tts/{name}";
                    list.Add(new { name, url });
                }
            }
            return Json(list);
        }

        public IActionResult TtsTest()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
