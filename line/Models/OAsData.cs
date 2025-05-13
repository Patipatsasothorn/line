namespace line.Models
{
    // Model สำหรับเก็บข้อมูล
    public class OAsData
    {
        public List<string> Tokens { get; set; }        // รับเป็น List ของ tokens
        public List<string> DisplayNames { get; set; }  // รับเป็น List ของ displayNames
        public List<string> BotIds { get; set; }        // รับเป็น List ของ botIds
    }
}
