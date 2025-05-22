namespace line.Models
{
    public class ReceiveChatHistoryRequest
    {
        public string[] data { get; set; }       // ตามของคุณคือ array ของชื่อที่ส่งมา
        public string userId { get; set; }
        public string botId { get; set; }
    }

}
