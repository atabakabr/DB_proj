using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace DormWeb.Controllers;

public class AuthController : Controller
{
    private readonly string _connStr;

    public AuthController(IConfiguration config)
    {
        _connStr = config.GetConnectionString("Default")!;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        using var conn = new SqlConnection(_connStr);

        string hashedPassword = HashPassword(password);

        var staff = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT PersonnelID, Password, UserRole FROM Staff WHERE OrganizationalEmail = @Email",
            new { Email = email });

        if (staff != null && (string)staff.Password == hashedPassword)
        {
            HttpContext.Session.SetInt32("UserId", (int)staff.PersonnelID);
            HttpContext.Session.SetString("UserRole", (string)staff.UserRole);

            return (string)staff.UserRole switch
            {
                "مدیر" => RedirectToAction("Index", "Manager"),
                "سرپرست" => RedirectToAction("Index", "Supervisor"),
                "نگهبان" => RedirectToAction("Index", "Guard"),
                _ => RedirectToAction("Login")
            };
        }

        var student = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT StudentID, Password FROM Student WHERE OrganizationalEmail = @Email",
            new { Email = email });

        if (student != null && (string)student.Password == hashedPassword)
        {
            HttpContext.Session.SetInt32("UserId", (int)student.StudentID);
            HttpContext.Session.SetString("UserRole", "Student");
            return RedirectToAction("Index", "Student");
        }

        ViewBag.Error = "ایمیل یا رمز عبور اشتباه است.";
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}