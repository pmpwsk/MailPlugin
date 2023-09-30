using MimeKit;
using System.Runtime.Serialization;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    [DataContract]
    public class MailMessage
    {
        [DataMember]
        public bool Unread;

        [DataMember]
        public readonly DateTime TimestampUtc;

        [DataMember]
        public readonly MailAddress From;

        [DataMember]
        public readonly MailAddress[] To;

        [DataMember]
        public readonly MailAddress[] Cc;

        [DataMember]
        public readonly MailAddress[] Bcc;

        [DataMember]
        public readonly MailAddress? ReplyTo;

        [DataMember]
        public readonly string MessageId;

        [DataMember]
        public readonly string? InReplyToId;

        [DataMember]
        public readonly string Subject;

        [DataMember]
        public readonly MailAttachment[] Attachments;

        [DataMember]
        public readonly MailAuthResult AuthResult;

        [DataMember]
        public readonly List<string> Log;

        public MailMessage(bool unread, DateTime timestampUtc, MimeMessage message, MailAttachment[] attachments, MailAuthResult authResult, List<string> log)
        {
            Unread = unread;
            TimestampUtc = timestampUtc;
            From = new(message.From.Mailboxes.FirstOrDefault() ?? throw new Exception("No sender found!"));
            To = message.To.Mailboxes.Select(x => new MailAddress(x)).ToArray();
            Cc = message.Cc.Mailboxes.Select(x => new MailAddress(x)).ToArray();
            Bcc = message.Bcc.Mailboxes.Select(x => new MailAddress(x)).ToArray();
            ReplyTo = message.ReplyTo.Mailboxes.Any() ? new(message.ReplyTo.Mailboxes.First()) : null;
            MessageId = message.MessageId;
            InReplyToId = message.InReplyTo;
            Subject = message.Subject;
            Attachments = attachments;
            AuthResult = authResult;
            Log = log;
        }
    }
}