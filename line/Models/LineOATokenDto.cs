namespace line.Models
{
    public class OaData
    {
        public string Username { get; set; }
        public string Userid { get; set; }
        public string Oaname { get; set; }
        public string AccessToken { get; set; }
        public DateTime Createdate { get; set; }
        public TimeSpan Createtime { get; set; }
        public string BotUserId { get; set; }  // <<-- เพิ่มช่องนี้ในโมเดล

    }
}
