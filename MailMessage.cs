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
        public List<MailAddress> To;

        [DataMember]
        public List<MailAddress> Cc;

        [DataMember]
        public List<MailAddress> Bcc;

        [DataMember]
        public MailAddress? ReplyTo;

        [DataMember]
        public string MessageId;

        [DataMember]
        public string? InReplyToId;

        [DataMember]
        public string Subject;

        [DataMember]
        public List<MailAttachment> Attachments;

        [DataMember]
        public MailAuthResult? AuthResult;

        [DataMember]
        public List<string> Log;

        public MailMessage(bool unread, DateTime timestampUtc, MimeMessage message, List<MailAttachment> attachments, MailAuthResult authResult, List<string> log)
        {
            Unread = unread;
            TimestampUtc = timestampUtc;
            From = new(message.From.Mailboxes.FirstOrDefault() ?? throw new Exception("No sender found!"));
            To = message.To.Mailboxes.Select(x => new MailAddress(x)).ToList();
            Cc = message.Cc.Mailboxes.Select(x => new MailAddress(x)).ToList();
            Bcc = message.Bcc.Mailboxes.Select(x => new MailAddress(x)).ToList();
            ReplyTo = message.ReplyTo.Mailboxes.Any() ? new(message.ReplyTo.Mailboxes.First()) : null;
            MessageId = message.MessageId;
            InReplyToId = message.InReplyTo;
            Subject = message.Subject;
            Attachments = attachments;
            AuthResult = authResult;
            Log = log;
        }

        public MailMessage(MailAddress from, List<MailAddress> to, string subject, string? inReplyToId)
        {
            Unread = false;
            TimestampUtc = DateTime.UtcNow;
            From = from;
            To = to;
            Cc = new();
            Bcc = new();
            ReplyTo = null;
            MessageId = "none";
            InReplyToId = inReplyToId;
            Subject = subject;
            Attachments = new();
            AuthResult = null;
            Log = new();
        }
    }
}