using line.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using line.Models;  // <-- ต้องมีบรรทัดนี้

namespace line.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _db;

        public AccountController(IConfiguration configuration, AppDbContext appDbContext)
        {
            _configuration = configuration;
            _db = appDbContext;
        }

        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // ตรวจสอบ username & password
                using (SqlCommand cmd = new SqlCommand("SELECT Type FROM UserLogin WHERE Username = @u AND Password = @p", conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", password);

                    var type = cmd.ExecuteScalar() as string;

                    if (!string.IsNullOrEmpty(type))
                    {
                        HttpContext.Session.SetString("Username", username);
                        HttpContext.Session.SetString("UserType", type);

                        // ✅ เพิ่ม: รวมเวลาที่ยังไม่หมดจาก UserKeys
                        string getTimeoutsQuery = @"
                    SELECT Timeout 
                    FROM UserKeys 
                    WHERE Useru = @u 
                      AND Timeout > GETDATE()";

                        using (SqlCommand timeoutCmd = new SqlCommand(getTimeoutsQuery, conn))
                        {
                            timeoutCmd.Parameters.AddWithValue("@u", username);
                            using (SqlDataReader reader = timeoutCmd.ExecuteReader())
                            {
                                TimeSpan totalRemaining = TimeSpan.Zero;

                                while (reader.Read())
                                {
                                    if (reader["Timeout"] != DBNull.Value)
                                    {
                                        var timeout = Convert.ToDateTime(reader["Timeout"]);
                                        var remaining = timeout - DateTime.Now;

                                        if (remaining > TimeSpan.Zero)
                                        {
                                            totalRemaining += remaining;
                                        }
                                    }
                                }

                                if (totalRemaining > TimeSpan.Zero)
                                {
                                    var finalTimeout = DateTime.Now.Add(totalRemaining);
                                    HttpContext.Session.SetString("KeyTimeout", finalTimeout.ToString("o")); // o = ISO format
                                }
                            }
                        }

                        // ✅ redirect
                        if (type.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
                            return RedirectToAction("Index", "Admin");

                        return RedirectToAction("Index", "Home");
                    }
                }
            }

            ViewBag.Error = "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง";
            return View();
        }

        //private TimeSpan ParseSetTime(string setTime)
        //{
        //    if (setTime.Contains("วัน"))
        //        return TimeSpan.FromDays(int.Parse(setTime.Replace("วัน", "").Trim()));
        //    if (setTime.Contains("ชม."))
        //        return TimeSpan.FromHours(int.Parse(setTime.Replace("ชม.", "").Trim()));
        //    if (setTime.Contains("นาที"))
        //        return TimeSpan.FromMinutes(int.Parse(setTime.Replace("นาที", "").Trim()));
        //    return TimeSpan.Zero;
        //}

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }
        public IActionResult LogoutPage()
        {
            return View();
        }

        // POST: /Account/LogoutFromWeb
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LogoutFromWeb()
        {
            // เคลียร์ session หรือ cookie ที่ใช้เก็บข้อมูล login ของเว็บ
            HttpContext.Session.Clear();

            // หรือถ้าคุณใช้ Authentication Middleware เช่น Cookie Authentication ให้ sign out
            // await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // redirect ไปหน้า login หรือหน้าอื่นตามต้องการ
            return RedirectToAction("Login", "Account");
        }
        [HttpPost]
        public IActionResult Register(string username, string password, string type = "user")
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "กรุณากรอกชื่อผู้ใช้และรหัสผ่าน";
                return View();
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // ตรวจสอบว่าชื่อผู้ใช้นี้มีอยู่แล้วหรือไม่
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM UserLogin WHERE Username = @username", conn);
                checkCmd.Parameters.AddWithValue("@username", username);
                int exists = (int)checkCmd.ExecuteScalar();

                if (exists > 0)
                {
                    ViewBag.Error = "ชื่อผู้ใช้นี้มีอยู่แล้ว กรุณาเลือกชื่ออื่น";
                    return View();
                }

                // บันทึกผู้ใช้ใหม่ พร้อมบันทึกประเภท
                var insertCmd = new SqlCommand("INSERT INTO UserLogin (Username, Password, Type) VALUES (@username, @password, @type)", conn);
                insertCmd.Parameters.AddWithValue("@username", username);
                insertCmd.Parameters.AddWithValue("@password", password);
                insertCmd.Parameters.AddWithValue("@type", type); // 👈 เพิ่ม type

                insertCmd.ExecuteNonQuery();
            }

            ViewBag.Success = "สมัครสมาชิกสำเร็จ! กรุณาเข้าสู่ระบบ";
            return View();
        }
        [HttpPost]
        public IActionResult RegisterAdmin(string username, string password, string type = "admin")
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "กรุณากรอกชื่อผู้ใช้และรหัสผ่าน";
                return View();
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // ตรวจสอบว่าชื่อผู้ใช้นี้มีอยู่แล้วหรือไม่
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM UserLogin WHERE Username = @username", conn);
                checkCmd.Parameters.AddWithValue("@username", username);
                int exists = (int)checkCmd.ExecuteScalar();

                if (exists > 0)
                {
                    ViewBag.Error = "ชื่อผู้ใช้นี้มีอยู่แล้ว กรุณาเลือกชื่ออื่น";
                    return View();
                }

                // บันทึกผู้ใช้ใหม่ พร้อมบันทึกประเภท admin
                var insertCmd = new SqlCommand("INSERT INTO UserLogin (Username, Password, Type) VALUES (@username, @password, @type)", conn);
                insertCmd.Parameters.AddWithValue("@username", username);
                insertCmd.Parameters.AddWithValue("@password", password);
                insertCmd.Parameters.AddWithValue("@type", type);

                insertCmd.ExecuteNonQuery();
            }

            ViewBag.Success = "สมัครสมาชิก Admin สำเร็จ! กรุณาเข้าสู่ระบบ";
            return View();
        }

        [HttpGet]
        public IActionResult RegisterAdmin()
        {
            return View();
        }
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
