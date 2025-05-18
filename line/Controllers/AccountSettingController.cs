using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace line.Controllers
{
    public class AccountSettingController : Controller
    {
        private readonly IConfiguration _configuration;

        public AccountSettingController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            ViewBag.Username = HttpContext.Session.GetString("Username");
            return View();
        }

        [HttpPost]
        public IActionResult SubmitKey(string key)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(key))
            {
                ViewBag.Error = "กรุณากรอก Key";
                return View("Index");
            }

            var connStr = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                var cmd = new SqlCommand("SELECT * FROM UserKeys WHERE [Key] = @key", conn);
                cmd.Parameters.AddWithValue("@key", key);
                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var existingUser = reader["Useru"]?.ToString();
                    if (!string.IsNullOrEmpty(existingUser))
                    {
                        reader.Close();
                        ViewBag.Error = "Key นี้มีผู้ใช้งานแล้ว";
                        ViewBag.Username = username;
                        return RedirectToAction("Index", "AccountSetting");
                    }

                    var setTimeStr = reader["SetTime"].ToString(); // เช่น "30วัน", "15นาที", "1ชม."
                    reader.Close();

                    // แปลง setTimeStr เป็น TimeSpan
                    TimeSpan duration;
                    if (!TryParseDuration(setTimeStr, out duration))
                    {
                        ViewBag.Error = "รูปแบบ SetTime ไม่ถูกต้อง";
                        ViewBag.Username = username;
                        return View("Index");
                    }

                    var now = DateTime.Now;
                    var timeout = now.Add(duration);

                    // อัพเดตข้อมูล
                    var updateCmd = new SqlCommand(
                        "UPDATE UserKeys SET Useru = @useru, CreateTime = @now, Timeout = @timeout WHERE [Key] = @key", conn);
                    updateCmd.Parameters.AddWithValue("@useru", username);
                    updateCmd.Parameters.AddWithValue("@now", now);
                    updateCmd.Parameters.AddWithValue("@timeout", timeout);
                    updateCmd.Parameters.AddWithValue("@key", key);
                    updateCmd.ExecuteNonQuery();

                    ViewBag.Success = "ผูก Key สำเร็จ!";
                    ViewBag.Timeout = timeout;
                }
                else
                {
                    ViewBag.Error = "ไม่พบ Key ที่คุณกรอก";
                }
            }

            ViewBag.Username = username;
            return View("Index");
        }

        /// <summary>
        /// แปลงข้อความที่เก็บระยะเวลา เช่น "30วัน", "15นาที", "1ชม." เป็น TimeSpan
        /// </summary>
        private bool TryParseDuration(string input, out TimeSpan duration)
        {
            duration = TimeSpan.Zero;
            input = input.Trim();

            try
            {
                if (input.EndsWith("วัน"))
                {
                    if (int.TryParse(input.Replace("วัน", ""), out int days))
                    {
                        duration = TimeSpan.FromDays(days);
                        return true;
                    }
                }
                else if (input.EndsWith("นาที"))
                {
                    if (int.TryParse(input.Replace("นาที", ""), out int minutes))
                    {
                        duration = TimeSpan.FromMinutes(minutes);
                        return true;
                    }
                }
                else if (input.EndsWith("ชม.") || input.EndsWith("ชั่วโมง"))
                {
                    string temp = input.Replace("ชม.", "").Replace("ชั่วโมง", "");
                    if (int.TryParse(temp, out int hours))
                    {
                        duration = TimeSpan.FromHours(hours);
                        return true;
                    }
                }
            }
            catch
            {
                // parse error
            }
            return false;
        }

    }

}
