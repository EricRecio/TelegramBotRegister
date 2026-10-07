using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Contact
    {
        public long Id { get; set; } 
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public int UnreadCount { get; set; }
        public string LastMessagePreview { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; }

        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
