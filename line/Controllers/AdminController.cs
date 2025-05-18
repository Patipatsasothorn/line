using line.Data;
using line.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace line.Controllers
{
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;
        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public IActionResult Index()
        {
            var userType = HttpContext.Session.GetString("UserType");
            if (string.IsNullOrWhiteSpace(userType) || userType.Trim().ToLower() != "admin")
            {
                return RedirectToAction("Login", "Account");
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var userKeys = new List<UserKeyModel>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // ดึง Useru และ Timeout เพิ่ม
                var cmd = new SqlCommand("SELECT [Key], UserCreate, CreateTime, SetTime, Useru, Timeout FROM UserKeys ORDER BY CreateTime DESC", conn);

                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    userKeys.Add(new UserKeyModel
                    {
                        Key = reader["Key"].ToString(),
                        UserCreate = reader["UserCreate"].ToString(),
                        CreateTime = Convert.ToDateTime(reader["CreateTime"]),
                        SetTime = reader["SetTime"].ToString(),

                        // แปลงค่า Useru และ Timeout
                        Useru = reader["Useru"] == DBNull.Value ? "" : reader["Useru"].ToString(),
                        Timeout = reader["Timeout"] == DBNull.Value
                                    ? ""
                                    : reader["Timeout"].ToString()
                    });
                }
            }

            ViewBag.UserKeys = userKeys;
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken] // ถ้าเปิดใช้ AntiForgery
        public IActionResult DeleteKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Json(new { success = false, message = "คีย์ไม่ถูกต้อง" });
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand("DELETE FROM UserKeys WHERE [Key] = @Key", conn);
                    cmd.Parameters.AddWithValue("@Key", key);

                    var rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        return Json(new { success = true });
                    }
                    else
                    {
                        return Json(new { success = false, message = "ไม่พบคีย์ในฐานข้อมูล" });
                    }
                }
            }
            catch (Exception ex)
            {
                // อาจบันทึก log เพิ่มเติม
                return Json(new { success = false, message = "เกิดข้อผิดพลาด: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // ถ้าคุณเปิดใช้ AntiForgery
        public IActionResult GenerateKey(string timeValue, string timeUnit)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var now = DateTime.Now;
            var datePrefix = "LA" + now.ToString("yyyyMMdd");

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                int running = 1;
                string newKey = "";

                while (true)
                {
                    newKey = $"{datePrefix}-{running.ToString("D4")}";
                    var checkCmd = new SqlCommand("SELECT COUNT(*) FROM UserKeys WHERE [Key] = @key", conn);
                    checkCmd.Parameters.AddWithValue("@key", newKey);

                    int exists = (int)checkCmd.ExecuteScalar();
                    if (exists == 0)
                        break;

                    running++;
                }

                var insertCmd = new SqlCommand(@"INSERT INTO UserKeys ([Key], UserCreate, CreateTime, SetTime) 
                                         VALUES (@key, @user, @createTime, @setTime)", conn);
                insertCmd.Parameters.AddWithValue("@key", newKey);
                insertCmd.Parameters.AddWithValue("@user", username);
                insertCmd.Parameters.AddWithValue("@createTime", now);
                insertCmd.Parameters.AddWithValue("@setTime", $"{timeValue} {timeUnit}");

                insertCmd.ExecuteNonQuery();

                return Json(new { success = true, key = newKey });
            }
        }


    }
}
