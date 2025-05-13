namespace line.Models
{
    public class ReplyFormDto
    {
        public string UserId { get; set; }
        public string Message { get; set; }
        public string BotId { get; set; }  // เพิ่ม BotId ใน DTO

    }
}
