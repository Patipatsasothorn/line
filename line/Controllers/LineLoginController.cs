using line.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace line.Controllers
{
    public class LineLoginController : Controller
    {
        private readonly string redirectUri = "https://lineoa.xcoptech.net/LineLogin/Callback";

        private readonly ClientidContext _clientidContext;
        private readonly IConfiguration _configuration;

        public LineLoginController(IConfiguration configuration, ClientidContext ClientidContext)
        {
            _configuration = configuration;
            _clientidContext = ClientidContext;
        }
        [HttpPost]
        public IActionResult SetClientTemp(string clientId, string clientSecret)
        {
            HttpContext.Session.SetString("CustomClientId", clientId);
            HttpContext.Session.SetString("CustomClientSecret", clientSecret);
            return Ok();
        }
        private (string clientId, string clientSecret) GetClientFromSession()
        {
            var clientId = HttpContext.Session.GetString("CustomClientId");
            var clientSecret = HttpContext.Session.GetString("CustomClientSecret");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                // fallback กรณีไม่มี session ใช้ค่าคงที่เดิม
                clientId = "2007354861";
                clientSecret = "43950a842e7a70c3108345ebb068aa76";
            }

            return (clientId, clientSecret);
        }

        public IActionResult Login()
        {
            var (clientId, _) = GetClientFromSession(); // เอา clientId จาก session
            var state = Guid.NewGuid().ToString();
            var scope = "profile openid email";
            var url = $"https://access.line.me/oauth2/v2.1/authorize?" +
                      $"response_type=code" +
                      $"&client_id={clientId}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&state={state}" +
                      $"&scope={scope}";

            return Redirect(url);
        }


        public async Task<IActionResult> Callback(string code, string state)
        {
            var (clientId, clientSecret) = GetClientFromSession(); // ดึงจาก session

            var client = new HttpClient();

            var tokenResponse = await client.PostAsync("https://api.line.me/oauth2/v2.1/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
            {"grant_type", "authorization_code"},
            {"code", code},
            {"redirect_uri", redirectUri},
            {"client_id", clientId},
            {"client_secret", clientSecret},
                }));

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            dynamic tokenData = JsonConvert.DeserializeObject(tokenJson);
            string accessToken = tokenData.access_token;

            // Get user profile
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var profileResponse = await client.GetAsync("https://api.line.me/v2/profile");
            var profileJson = await profileResponse.Content.ReadAsStringAsync();
            dynamic profile = JsonConvert.DeserializeObject(profileJson);

            string userId = profile.userId;
            string displayName = profile.displayName;
            string pictureUrl = profile.pictureUrl;

            HttpContext.Session.SetString("LINE_UserId", userId);
            HttpContext.Session.SetString("LINE_DisplayName", displayName);
            HttpContext.Session.SetString("LINE_PictureUrl", pictureUrl);

            return RedirectToAction("alllineoa", "Home");
        }

        [HttpPost]
        public IActionResult SaveLive(KewLiveModel model)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            var userId = HttpContext.Session.GetString("Username");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"INSERT INTO KEW_Live (Name, ClientId, ClientSecret, Userid)
                             VALUES (@Name, @ClientId, @ClientSecret, @Userid)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", userId);
                    cmd.Parameters.AddWithValue("@ClientId", model.ClientId);
                    cmd.Parameters.AddWithValue("@ClientSecret", model.ClientSecret);
                    cmd.Parameters.AddWithValue("@Userid", model.Name ?? "anonymous");

                    conn.Open();
                    int rowsAffected = cmd.ExecuteNonQuery();
                    conn.Close();

                    if (rowsAffected > 0)
                    {
                        return Json(new { success = true });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Insert failed." });
                    }
                }
            }
        }
        [HttpGet]
        public IActionResult oatable()
        {
            var userId = HttpContext.Session.GetString("Username");

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(); // หรือ return Json(new { error = "No user session" });
            }

            var result = _clientidContext.KewLiveModels
                          .FromSqlInterpolated($"EXEC Clientid {userId}")
                          .ToList();

            return Json(result);
        }

        public IActionResult Logout()
        {
            // ลบข้อมูล session ที่เกี่ยวข้องกับ LINE
            HttpContext.Session.Remove("LINE_UserId");
            HttpContext.Session.Remove("LINE_DisplayName");
            HttpContext.Session.Remove("LINE_PictureUrl");

            return RedirectToAction("Index", "Home"); // หรือหน้าอื่นที่ต้องการ
        }

    }

}
