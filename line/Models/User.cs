namespace line.Models
{
    public class User
    {
        public int Id { get; set; }  // ถ้ามี PK เป็น int
        public string Username { get; set; }
        public string Password { get; set; }
        public string Type { get; set; }  // เช่น "user" หรือ "admin"
    }
}
