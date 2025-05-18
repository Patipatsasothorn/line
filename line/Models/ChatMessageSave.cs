namespace line.Models
{
    public class ChatMessageSave
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string BotId { get; set; }
        public string SenderType { get; set; }  // "user" หรือ "bot"
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsImage { get; set; }
    }

}
