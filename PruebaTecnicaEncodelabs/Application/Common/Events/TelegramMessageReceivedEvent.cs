using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.Events
{
    public record TelegramMessageReceivedEvent(
    long ChatId,
    string Username,
    string FirstName,
    string Text,
    DateTime SentAt);
}
