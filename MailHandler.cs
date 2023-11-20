using MimeKit;
using Org.BouncyCastle.Ocsp;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System.Diagnostics.CodeAnalysis;
using uwap.WebFramework.Mail;
using static uwap.WebFramework.Mail.MailAuth;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private bool BeforeSend(MailGen mailGen, MailboxAddress currentRecipient, string potentialMessageId, [MaybeNullWhen(true)] out string log)
    {
        if (!Mailboxes.MailboxByAddress.TryGetValue(currentRecipient.Address, out var mailbox))
        {
            if (SendMissingInternalRecipientsExternally || !Mailboxes.MailboxByAddress.Keys.Any(x => x.EndsWith('@' + currentRecipient.Domain)))
            {
                log = null;
                return true;
            }
            else
            {
                log = "Address not found.";
                return false;
            }    
        }

        mailbox.Lock();
        ulong messageId = (ulong)DateTime.UtcNow.Ticks;
        while (mailbox.Messages.ContainsKey(messageId))
            messageId++;
        mailbox.Messages[messageId] = new(true, DateTime.UtcNow, mailGen, potentialMessageId);
        mailbox.Folders["Inbox"].Add(messageId);
        mailbox.UnlockSave();
        Directory.CreateDirectory($"../Mail/{mailbox.Id}/{messageId}");
        if (mailGen.TextBody != null)
            File.WriteAllText($"../Mail/{mailbox.Id}/{messageId}/text", mailGen.TextBody);
        if (mailGen.HtmlBody != null)
            File.WriteAllText($"../Mail/{mailbox.Id}/{messageId}/html", mailGen.HtmlBody);
        int attachmentIndex = 0;
        foreach (var attachment in mailGen.Attachments)
        {
            File.WriteAllBytes($"../Mail/{mailbox.Id}/{messageId}/{attachmentIndex}", attachment.Bytes);
            attachmentIndex++;
        }
        if (IncomingListeners.TryGetValue(mailbox, out var listenerKV))
            foreach (var listenerKKV in listenerKV)
                try
                {
                    listenerKKV.Key.Send(listenerKKV.Value ? "refresh" : "icon").GetAwaiter().GetResult();
                }
                catch
                {
                    foreach (var kv in IncomingListeners)
                        if (kv.Value.Remove(listenerKKV.Key) && kv.Value.Count == 0)
                            IncomingListeners.Remove(kv.Key);
                }
        log = "Sent internally.";
        return false;
    }

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
            List<string> log = [];
            FullResult authResult = CheckEverything(connectionData, message, log);
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
                bool happy = connectionData.Secure && mailbox.AuthRequirements.SatisfiedBy(authResult);
                mailbox.Folders[happy ? "Inbox" : "Spam"].Add(messageId);
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
                if (IncomingListeners.TryGetValue(mailbox, out var listenerKV))
                    foreach (var listenerKKV in listenerKV)
                        try
                        {
                            listenerKKV.Key.Send(listenerKKV.Value ? "refresh" : "icon").GetAwaiter().GetResult();
                        }
                        catch
                        {
                            foreach (var kv in IncomingListeners)
                                if (kv.Value.Remove(listenerKKV.Key) && kv.Value.Count == 0)
                                    IncomingListeners.Remove(kv.Key);
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