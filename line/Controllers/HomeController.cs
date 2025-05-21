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
using Microsoft.Data.SqlClient;
using Dapper;

namespace line.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpClientFactory, IHubContext<ChatHub> hubContext, IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;
            _configuration = configuration;

        }

        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        public IActionResult alllineoa()
        {


            return View();
        }

        public IActionResult Privacy()
        {
            var chats = ChatStorage.GetAll();
            var oaAccounts = OAAccountStorage.GetAll();
            

            ViewBag.OAAccounts = oaAccounts;

            return View(chats);
        }
        [HttpPost]
        [Route("line/webhook")]
        public async Task<IActionResult> LineWebhook()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(body);

            // ดึง userId จาก Session
            string BotId = data.destination;
            if (string.IsNullOrEmpty(BotId))
            {
                // ถ้าไม่มี userId ใน session
                return BadRequest("UserId not found in session.");
            }
            // ดึงข้อมูลจาก webhook
            foreach (var evt in data.events)
            {
                string type = evt.type;
                if (type == "message")
                {
                    string messageType = evt.message.type;
                    string userId = evt.source.userId;
                    string replyToken = evt.replyToken;
                    var profile = await GetUserProfile(userId, BotId);
                    var timestamp = DateTime.Now.ToString("HH:mm");

                    var oaInfo = GetOAInfoFromDatabase(BotId);
                    string oaName = oaInfo?.Oaname ?? "Unknown OA";

                    string message = "";

                    if (messageType == "text")
                    {
                        message = evt.message.text;
                    }
                    else if (messageType == "image")
                    {
                        // โหลดไฟล์ภาพจาก LINE server ด้วย message.id
                        string messageId = evt.message.id;
                        byte[] imageData = await GetContentFromLineMessage(messageId, BotId);

                        // ตั้งชื่อไฟล์แบบสุ่ม
                        string fileName = $"{Guid.NewGuid()}.jpg";
                        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                        // สร้างโฟลเดอร์ถ้ายังไม่มี
                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }

                        // บันทึกไฟล์ลง local
                        string filePath = Path.Combine(folderPath, fileName);
                        await System.IO.File.WriteAllBytesAsync(filePath, imageData);

                        // สร้าง URL สำหรับแสดงบนเว็บ (เปลี่ยนให้ตรงกับ Host จริงถ้า Deploy แล้ว)
                        string imageUrl = $"https://localhost:7271/uploads/{fileName}";

                        // ส่ง URL แทนข้อความ
                        message = imageUrl;


                        // คุณอาจจะเก็บ imageData หรือ upload ไปยัง storage แล้วส่งลิงก์ให้ front-end
                    }
                
                        else if (messageType == "sticker")
                        {
                            string packageId = evt.message.packageId;
                            string stickerId = evt.message.stickerId;

                            // สร้าง URL สำหรับแสดงสติกเกอร์ (LINE ไม่มี public URL โดยตรง)
                            // อาจแสดงข้อความแทน เช่น
                            message = $"[Sticker] packageId: {packageId}, stickerId: {stickerId}";

                        // หรือเลือกแสดงภาพตัวอย่าง (Preview) เช่นจาก LINE CDN:
                        message = $"https://stickershop.line-scdn.net/stickershop/v1/sticker/{stickerId}/ANDROID/sticker.png";
                    }

                    ChatStorage.Add(userId, message);
                    await SaveChatToDatabase(userId, profile.DisplayName, message, profile.PictureUrl, timestamp, oaName, BotId);

                    await _hubContext.Clients.All.SendAsync(
                        "ReceiveMessage",
                        profile.DisplayName,
                        message,
                        profile.PictureUrl,
                        timestamp,
                        userId,
                        oaName,
                        BotId
                    );

                    await _hubContext.Clients.All.SendAsync(
                        "UpdateChatList",
                        userId,
                        profile.DisplayName,
                        profile.PictureUrl,
                        message,
                        timestamp,
                        oaName,
                        BotId
                    );
                }


            }

            return Ok();
        }
        [HttpPost]
        public async Task<IActionResult> GetChatHistories([FromBody] ChatHistoryRequest request)
        {
            if (request == null || request.DisplayNames == null || request.DisplayNames.Length == 0)
            {
                return BadRequest("No display names provided.");
            }

            // ตัดช่องว่างหน้าหลัง (Trim) ทุกตัวใน array
            var trimmedDisplayNames = request.DisplayNames
                .Where(name => !string.IsNullOrWhiteSpace(name))  // กรองชื่อว่าง หรือมีแต่ช่องว่างทิ้ง
                .Select(name => name.Trim())
                .ToArray();

            if (trimmedDisplayNames.Length == 0)
            {
                return BadRequest("Display names are empty after trimming.");
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);

            var query = @"
        SELECT TOP 100 Id, UserId, OAName, BotId, Message, Timestamp, DisplayName, PictureUrl
        FROM dbo.ChatHistory
        WHERE OAName IN @DisplayNames
        ORDER BY Timestamp DESC";

            var chats = await connection.QueryAsync<ChatHistory>(
                query,
                new { DisplayNames = trimmedDisplayNames }
            );

            foreach (var chat in chats)
            {
                await _hubContext.Clients.All.SendAsync(
                    "ReceiveMessage",
                    chat.DisplayName,
                    chat.Message,
                    chat.PictureUrl,
                    chat.Timestamp.ToString("HH:mm"),
                    chat.UserId,
                    chat.OAName,
                    chat.BotId
                );

                await _hubContext.Clients.All.SendAsync(
                    "UpdateChatList",
                    chat.UserId,
                    chat.DisplayName,
                    chat.PictureUrl,
                    chat.Message,
                    chat.Timestamp.ToString("HH:mm"),
                    chat.OAName,
                    chat.BotId
                );
            }

            return Json(new { success = true, count = chats.AsList().Count });
        }
        private async Task SaveChatToDatabase(string userId, string displayName, string message, string pictureUrl, string timestamp, string oaName, string botId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string sql = @"
            INSERT INTO KEW_Live.dbo.ChatHistory 
            (UserId, DisplayName, Message, PictureUrl, Timestamp, OAName, BotId)
            VALUES (@UserId, @DisplayName, @Message, @PictureUrl, @Timestamp, @OAName, @BotId)";

                using (var command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@DisplayName", displayName);
                    command.Parameters.AddWithValue("@Message", message);
                    command.Parameters.AddWithValue("@PictureUrl", pictureUrl ?? "");
                    command.Parameters.AddWithValue("@Timestamp", DateTime.ParseExact(timestamp, "HH:mm", null)); // หรือ DateTime.Now
                    command.Parameters.AddWithValue("@OAName", oaName ?? "");
                    command.Parameters.AddWithValue("@BotId", botId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        [HttpGet]

        public async Task<IActionResult> LoadChatHistory(string[] displayNames)
        {
            var chatHistory = new List<object>();

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                await connection.OpenAsync();

                foreach (var name in displayNames)
                {
                    string sql = @"SELECT * FROM KEW_Live.dbo.ChatHistory WHERE DisplayName = @DisplayName ORDER BY Timestamp ASC";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@DisplayName", name);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                chatHistory.Add(new
                                {
                                    UserId = reader["UserId"].ToString(),
                                    DisplayName = reader["DisplayName"].ToString(),
                                    Message = reader["Message"].ToString(),
                                    PictureUrl = reader["PictureUrl"].ToString(),
                                    Timestamp = ((DateTime)reader["Timestamp"]).ToString("HH:mm"),
                                    OAName = reader["OAName"].ToString(),
                                    BotId = reader["BotId"].ToString()
                                });
                            }
                        }
                    }
                }
            }

            return Json(chatHistory);
        }

        public async Task<byte[]> GetContentFromLineMessage(string messageId, string BotId)
        {
            string accessToken = await GetAccessTokenFromDatabase(BotId);
            if (string.IsNullOrEmpty(accessToken)) return null;

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"https://api-data.line.me/v2/bot/message/{messageId}/content");
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<string> GetAccessTokenFromDatabase(string BotId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(@"
            SELECT AccessToken 
            FROM KEW_Live.dbo.line 
            WHERE BotUserId = @UserId", connection))
                {
                    command.Parameters.AddWithValue("@UserId", BotId);

                    var result = await command.ExecuteScalarAsync();
                    return result?.ToString();
                }
            }
        }

        private OaData GetOAInfoFromDatabase(string userId)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqlConnection(connectionString)) // ใส่ ConnectionString ที่เชื่อมกับฐานข้อมูลของคุณ
            {
                connection.Open();

                // สร้าง SQL Query เพื่อดึงข้อมูล Oaname จากฐานข้อมูล KEW_Live.dbo.line
                var query = @"
            SELECT Oaname
            FROM KEW_Live.dbo.line
            WHERE BotUserId = @UserId";

                // ใช้ SqlCommand เพื่อ execute SQL
                using (var command = new SqlCommand(query, connection))
                {
                    // เพิ่ม parameter userId เพื่อป้องกัน SQL Injection
                    command.Parameters.AddWithValue("@UserId", userId);

                    // ดึงผลลัพธ์จากฐานข้อมูล
                    var result = command.ExecuteScalar();

                    // ถ้าหา Oaname ได้ คืนค่าเป็น LineOATokenDto
                    if (result != null)
                    {
                        return new OaData
                        {
                            Oaname = result.ToString()
                        };
                    }
                    else
                    {
                        return null; // ถ้าไม่พบข้อมูลก็คืนค่า null
                    }
                }
            }
        }


        public static class UserStorage
        {
            private static Dictionary<string, string> _userIds = new Dictionary<string, string>();

            public static void AddUser(string userId, string replyToken)
            {
                _userIds[userId] = replyToken; // à¡çº replyToken µÒÁ userId
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
        public async Task<IActionResult> SendReply([FromForm] ReplyFormDto dto, IFormFile imageFile)
        {
            // ส่งข้อความ
            if (!string.IsNullOrEmpty(dto.Message))
            {
                await PushMessage(dto.UserId, dto.Message, dto.BotId);
            }
            // ส่งสติกเกอร์ (ถ้ามี)
            else if (dto.StickerPackageId.HasValue && dto.StickerId.HasValue)
            {
                await PushSticker(dto.UserId, dto.BotId, dto.StickerPackageId.Value, dto.StickerId.Value);
            }

            // ส่งรูปภาพถ้ามี
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var imageUrl = $"{baseUrl}/uploads/{fileName}";

                await PushImage(dto.UserId, imageUrl, dto.BotId);
            }

            return Ok();
        }

        private async Task PushSticker(string userId, string botId, int packageId, int stickerId)
        {
            var accessToken = await GetAccessTokenFromDatabase(botId);
            if (string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException("Access token not found for botId.");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var payload = new
            {
                to = userId,
                messages = new[]
                {
            new
            {
                type = "sticker",
                packageId = packageId.ToString(),
                stickerId = stickerId.ToString()
            }
        }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
        }
        private async Task PushMessage(string userId, string message, string botId)
        {
            var accessToken = await GetAccessTokenFromDatabase(botId);
            if (string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException("Access token not found for botId.");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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
        private async Task PushImage(string userId, string imageUrl, string botId)
        {
            var accessToken = await GetAccessTokenFromDatabase(botId);
            if (string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException("Access token not found for botId.");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var payload = new
            {
                to = userId,
                messages = new[]
                {
            new
            {
                type = "image",
                originalContentUrl = imageUrl,
                previewImageUrl = imageUrl
            }
        }
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
        }

        private async Task<LineUserProfile> GetUserProfile(string userId ,string botId)
        {
            string accessToken = await GetAccessTokenFromDatabase(botId);

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
