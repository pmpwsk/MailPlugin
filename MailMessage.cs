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
        public DateTime TimestampUtc;

        [DataMember]
        public MailAddress From;

        [DataMember]
        public MailAddress[] To;

        [DataMember]
        public MailAddress[] Cc;

        [DataMember]
        public MailAddress[] Bcc;

        [DataMember]
        public MailAddress? ReplyTo;

        [DataMember]
        public string MessageId;

        [DataMember]
        public string? InReplyToId;

        [DataMember]
        public string Subject;

        [DataMember]
        public MailAttachment[] Attachments;

        [DataMember]
        public MailAuthResult AuthResult;

        [DataMember]
        public List<string> Log;

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