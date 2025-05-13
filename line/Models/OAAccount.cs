using System.Collections.Generic;

namespace line.Models
{
    public static class OAAccountStorage
    {
        private static readonly List<OAAccount> _accounts = new();

        public static void Add(OAAccount account)
        {
            _accounts.Add(account);
        }

        public static List<OAAccount> GetAll()
        {
            return _accounts;
        }

        public static void Clear()
        {
            _accounts.Clear(); // สำหรับทดสอบ ล้างข้อมูลทั้งหมด
        }
    }

    public class OAAccount
    {
        public string Name { get; set; }
        public string ChannelAccessToken { get; set; }
    }
}
