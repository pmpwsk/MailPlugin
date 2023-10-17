using MimeKit;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public MailboxFilterResult AcceptMail(ISessionContext context, IMailbox from, IMailbox to)
    {
        //Option 1: check if any addresses with that host domain exist (so that spammers/attackers can't scan for what mailboxes exist for a given domain)
        if (Mailboxes.MailboxByAddress.Keys.Any(x => x.EndsWith('@' + to.Host)))
            return MailboxFilterResult.Yes;

        //Option 2: check if that exact recipient address exists
        /*if (Mailboxes.MailboxByAddress.ContainsKey(from.AsAddress()))
            return MailboxFilterResult.Yes;*/

        return MailboxFilterResult.NoPermanently;
    }

    public SmtpResponse HandleMail(ISessionContext context, MimeMessage message, MailConnectionData connectionData)
    {
        MailboxAddress? from = message.From.Mailboxes.FirstOrDefault();
        if (from == null || !message.To.Mailboxes.Any())
            return SmtpResponse.SyntaxError;
        IEnumerable<MailboxAddress> mailboxes = message.To.Mailboxes.Union(message.Cc.Mailboxes).Union(message.Bcc.Mailboxes).Where(x => Mailboxes.MailboxByAddress.ContainsKey(x.Address));
        if (mailboxes.Any())
        {
            List<string> log = new();
            MailAuthResult authResult = new(connectionData, message, log);
            List<MailAttachment> attachments = message.Attachments.Select(x => new MailAttachment(x.ContentDisposition.FileName, x.ContentType.MimeType)).ToList();
            MailMessage mail = new(true, DateTime.UtcNow, message, attachments, authResult, log);
            foreach (string toAddress in mailboxes.Select(x => x.Address))
            {
                Mailbox mailbox = Mailboxes.MailboxByAddress[toAddress];
                mailbox.Lock();
                ulong messageId = (ulong)mail.TimestampUtc.Ticks;
                while (mailbox.Messages.ContainsKey(messageId))
                    messageId++;
                mailbox.Messages[messageId] = mail;
                mailbox.Folders[mailbox.AuthRequirements.SatisfiedBy(authResult) ? "Inbox" : "Spam"].Add(messageId);
                mailbox.UnlockSave();
                Directory.CreateDirectory($"../Mail/{mailbox.Id}/{messageId}");
                if (message.TextBody != null)
                    File.WriteAllText($"../Mail/{mailbox.Id}/{messageId}/text", message.TextBody);
                if (message.HtmlBody != null)
                    File.WriteAllText($"../Mail/{mailbox.Id}/{messageId}/html", message.HtmlBody);
                int attachmentIndex = 0;
                foreach (var attachment in message.Attachments)
                {
                    using var stream = File.OpenWrite($"../Mail/{mailbox.Id}/{messageId}/{attachmentIndex}");
                    ((MimePart)attachment).Content.DecodeTo(stream);
                    stream.Flush();
                    stream.Close();
                    stream.Dispose();
                    attachmentIndex++;
                }
            }
        }
        else if (PrintUnrecognizedToConsole)
        {
            MailboxAddress to = message.To.Mailboxes.FirstOrDefault() ?? new("NO RECIPIENT", "null@example.com");
            Console.WriteLine();
            Console.WriteLine($"UNRECOGNIZED MAIL (Secure={connectionData.Secure}, Host={connectionData.IP.Address}) '{message.Subject}' from {from.Name} ({from.Address}) to {to.Name} ({to.Address})");
            if (message.TextBody != null)
            {
                Console.WriteLine("TEXT:");
                foreach (var line in message.TextBody.Split('\n'))
                    Console.WriteLine("\t" + line);
            }
            if (message.HtmlBody != null)
            {
                Console.WriteLine("HTML:");
                foreach (var line in message.HtmlBody.Split('\n'))
                    Console.WriteLine("\t" + line);
            }
            if (message.Attachments.Any())
            {
                Console.WriteLine("ATTACHMENTS:");
                foreach (var attachment in message.Attachments)
                    Console.WriteLine($"\t{attachment.ContentDisposition.FileName} - {attachment.ContentType.MimeType}");
            }
            Console.WriteLine();
        }
        return SmtpResponse.Ok;
    }
}