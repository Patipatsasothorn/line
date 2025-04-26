using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using line.Models;
using System.Net.Http.Headers;
using line.Hubs;
using Microsoft.AspNetCore.SignalR;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace line.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<ChatHub> _hubContext;

        public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpClientFactory, IHubContext<ChatHub> hubContext)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;

        }

        public IActionResult Index()
        {
            var chats = ChatStorage.GetAll();
            return View(chats);
        }

        [HttpPost]
        [Route("line/webhook")]
        public async Task<IActionResult> LineWebhook()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(body);

            foreach (var evt in data.events)
            {
                string type = evt.type;
                if (type == "message")
                {
                    string userId = evt.source.userId;
                    string message = evt.message.text;
                    string replyToken = evt.replyToken;
                    var profile = await GetUserProfile(userId);
                    var timestamp = DateTime.Now.ToString("HH:mm");

                    ChatStorage.Add(userId, message);

                   // await ReplyMessage(replyToken, $"คุณพิมพ์ว่า: {message}");
                    await _hubContext.Clients.All.SendAsync("ReceiveMessage", profile.DisplayName, message, profile.PictureUrl, timestamp, userId);
                    await _hubContext.Clients.All.SendAsync(
                        "UpdateChatList",
                        userId,
                        profile.DisplayName,
                        profile.PictureUrl,
                        message,
                        timestamp
                    );

                }
            }

            return Ok();
        }
        public static class UserStorage
        {
            private static Dictionary<string, string> _userIds = new Dictionary<string, string>();

            public static void AddUser(string userId, string replyToken)
            {
                _userIds[userId] = replyToken; // เก็บ replyToken ตาม userId
            }

            public static string GetUserToken(string userId)
            {
                return _userIds.TryGetValue(userId, out var token) ? token : null;
            }

            public static void RemoveUser(string userId)
            {
                _userIds.Remove(userId);
            }
        }

        [HttpPost("send-reply")]
        public async Task<IActionResult> SendReply([FromBody] ReplyFormDto dto)
        {

            await PushMessage(dto.UserId, dto.Message);
            return Ok();
        }
        private async Task PushMessage(string userId, string message)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "/5RNFdq86shmnTbUQ99J62GHEJ5T0hrKVEYSVQ2G+o1m66qoP4dtpzRab/tGXOANRLD1SoOko1cLwDRPXaOgBTYwoanXS/8L8etChPbIPZ4OSxUkPmIQHWdtj+YgDJd1qn6RPrSZz6LdjfFyk1JW7AdB04t89/1O/w1cDnyilFU=");

            var payload = new
            {
                to = userId,
                messages = new[]
                {
            new { type = "text", text = message }
        }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
        }
        private async Task<LineUserProfile> GetUserProfile(string userId)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "/5RNFdq86shmnTbUQ99J62GHEJ5T0hrKVEYSVQ2G+o1m66qoP4dtpzRab/tGXOANRLD1SoOko1cLwDRPXaOgBTYwoanXS/8L8etChPbIPZ4OSxUkPmIQHWdtj+YgDJd1qn6RPrSZz6LdjfFyk1JW7AdB04t89/1O/w1cDnyilFU=");

            var response = await client.GetAsync($"https://api.line.me/v2/bot/profile/{userId}");
            if (!response.IsSuccessStatusCode)
                return new LineUserProfile { DisplayName = userId, PictureUrl = "" };

            var content = await response.Content.ReadAsStringAsync();
            dynamic profile = JsonConvert.DeserializeObject(content);

            return new LineUserProfile
            {
                DisplayName = profile.displayName,
                PictureUrl = profile.pictureUrl
            };
        }

        private async Task ReplyMessage(string replyToken, string message)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "/5RNFdq86shmnTbUQ99J62GHEJ5T0hrKVEYSVQ2G+o1m66qoP4dtpzRab/tGXOANRLD1SoOko1cLwDRPXaOgBTYwoanXS/8L8etChPbIPZ4OSxUkPmIQHWdtj+YgDJd1qn6RPrSZz6LdjfFyk1JW7AdB04t89/1O/w1cDnyilFU=");

            var payload = new
            {
                replyToken = replyToken,
                messages = new[]
                {
                    new { type = "text", text = message }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            await client.PostAsync("https://api.line.me/v2/bot/message/reply", content);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
