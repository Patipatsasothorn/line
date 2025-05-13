using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using line.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace line.Controllers
{
    public class LineOAController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly oatablecontext _oatablecontext;

        public LineOAController(IConfiguration configuration, oatablecontext oatablecontext)
        {
            _configuration = configuration;
            _oatablecontext = oatablecontext;
        }
        [HttpPost]
        public async Task<IActionResult> GetLineOAProfile([FromBody] string accessToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync("https://api.line.me/v2/bot/info");
            if (!response.IsSuccessStatusCode)
                return BadRequest("ไม่สามารถเรียก API ได้");

            var content = await response.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(content);

            return Json(new
            {
                displayName = (string)data.displayName,
                userId = (string)data.userId,
                basicId = (string)data.basicId
            });
        }
        [HttpGet]
        public IActionResult oatable(string userid)
        {

            var query = _oatablecontext.oatablemodels
                        .FromSqlInterpolated($"EXEC oatable {userid}")
                        .ToList();

            return Json(query);
        }
        [HttpPost]
        public async Task<IActionResult> SaveOaData([FromBody] List<OaData> oaData)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                foreach (var item in oaData)
                {
                    var query = @"
                INSERT INTO KEW_Live.dbo.line 
                (UserId, Username, Oaname, AccessToken, BotUserId, Createdate, Createtime) 
                VALUES (@UserId, @Username, @OaName, @Token, @BotUserId, @Createdate, @Createtime)";

                    var parameters = new
                    {
                        UserId = item.Userid,
                        Username = item.Username,
                        OaName = item.Oaname,
                        Token = item.AccessToken,
                        BotUserId = item.BotUserId,
                        Createdate = DateTime.Now.Date,             // เฉพาะวันที่
                        Createtime = DateTime.Now.TimeOfDay         // เฉพาะเวลา
                    };

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.ExecuteAsync(query, parameters);
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest($"เกิดข้อผิดพลาด: {ex.Message}");
            }
        }


    }
}
