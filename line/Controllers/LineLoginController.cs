using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace line.Controllers
{
    public class LineLoginController : Controller
    {
        private readonly string clientId = "2007354861";
        private readonly string clientSecret = "43950a842e7a70c3108345ebb068aa76";
        private readonly string redirectUri = "https://lineoa.xcoptech.net/LineLogin/Callback";

        public IActionResult Login()
        {
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

            // ดึงข้อมูลผู้ใช้
            string userId = profile.userId;
            string displayName = profile.displayName;
            string pictureUrl = profile.pictureUrl;

            // เซฟใน session หรือ auth login แล้ว redirect
            HttpContext.Session.SetString("LINE_UserId", userId);
            HttpContext.Session.SetString("LINE_DisplayName", displayName);
            HttpContext.Session.SetString("LINE_PictureUrl", pictureUrl);

            return RedirectToAction("alllineoa", "Home");
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
