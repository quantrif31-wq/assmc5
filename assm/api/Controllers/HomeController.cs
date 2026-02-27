using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Lab4.Models;

namespace Lab4.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Welcome()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }
    public IActionResult Contact()
    {
        // Bạn có thể truyền Model nếu cần, hoặc chỉ return View()
        var model = new lab4.Views.Home.ContactModel();
        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
