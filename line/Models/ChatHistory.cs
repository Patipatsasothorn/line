namespace line.Models
{
    public class ChatHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string OAName { get; set; }
        public string BotId { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public string DisplayName { get; set; }
        public string PictureUrl { get; set; }
    }

}
