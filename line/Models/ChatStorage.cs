using System;
using System.Collections.Generic;
using System.Linq;

namespace line.Models
{
    public static class ChatStorage
    {
        private static readonly List<ChatMessage> messages = new();

        public static void Add(string userId, string text)
        {
            messages.Add(new ChatMessage
            {
                UserId = userId,
                Text = text,
                Time = DateTime.Now
            });
        }

        public static List<ChatMessage> GetAll()
        {
            return messages.OrderByDescending(m => m.Time).ToList();
        }
    }

    public class ChatMessage
    {
        public string UserId { get; set; }
        public string Text { get; set; }
        public DateTime Time { get; set; }
    }
}
