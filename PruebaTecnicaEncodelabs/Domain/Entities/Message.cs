using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Message
    {
        public Guid Id { get; set; }
        public long ContactId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsFromOperator { get; set; }
        public bool IsRead { get; set; }

        public Contact Contact { get; set; } = null!;
    }
}
