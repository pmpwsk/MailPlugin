using MimeKit;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Protocol;
using System.Diagnostics.CodeAnalysis;
using uwap.Database;
using uwap.WebFramework.Mail;
using static uwap.WebFramework.Mail.MailAuth;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    private bool BeforeSend(MailGen mailGen, MailboxAddress currentRecipient, string potentialMessageId, [MaybeNullWhen(true)] out string log)
    {
        var mailboxId = Mailboxes.AddressIndex.Get(currentRecipient.Address);
        if (mailboxId == null)
        {
            if (SendMissingInternalRecipientsExternally || !Mailboxes.AddressIndex.Keys.Any(x => x.EndsWith('@' + currentRecipient.Domain)))
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

        Mailboxes.Transaction(mailboxId, (ref Mailbox mailbox, ref List<IFileAction> actions) =>
        {
            ulong messageId = (ulong)DateTime.UtcNow.Ticks;
            while (mailbox.Messages.ContainsKey(messageId))
                messageId++;
            mailbox.Messages[messageId] = new(true, DateTime.UtcNow, mailGen, potentialMessageId);
            mailbox.Folders["Inbox"].Add(messageId);
            if (mailGen.TextBody != null)
                actions.Add(new SetFileAction($"{messageId}/text", path => File.WriteAllText(path, mailGen.TextBody)));
            if (mailGen.HtmlBody != null)
                actions.Add(new SetFileAction($"{messageId}/html", path => File.WriteAllText(path, mailGen.HtmlBody)));
            int attachmentIndex = 0;
            foreach (var attachment in mailGen.Attachments)
            {
                actions.Add(new SetFileAction($"{messageId}/{attachmentIndex}", path => File.WriteAllBytes(path, attachment.Bytes)));
                attachmentIndex++;
            }
        });
        if (IncomingListeners.TryGetValue(mailboxId, out var listenerKV))
            foreach (var listenerKKV in listenerKV)
                try
                {
                    listenerKKV.Key.EventMessage(listenerKKV.Value ? "refresh" : "icon").GetAwaiter().GetResult();
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

    public bool MailboxExists(ISessionContext context, IMailbox from, IMailbox to)
    {
        //Option 1: check if any addresses with that host domain exist (so that spammers/attackers can't scan for what mailboxes exist for a given domain)
        return Mailboxes.AddressIndex.Keys.Any(x => x.EndsWith('@' + to.Host));

        //Option 2: check if that exact recipient address exists
        //return Mailboxes.AddressIndex.Get(from.AsAddress()) != null;
    }

    public SmtpResponse HandleMail(ISessionContext context, MimeMessage message, MailConnectionData connectionData)
    {
        MailboxAddress? from = message.From.Mailboxes.FirstOrDefault();
        if (from == null || !message.To.Mailboxes.Any())
            return SmtpResponse.SyntaxError;
        var mailboxes = message.To.Mailboxes.Union(message.Cc.Mailboxes).Union(message.Bcc.Mailboxes).Where(x => Mailboxes.AddressIndex.Get(x.Address) != null).ToList();
        if (mailboxes.Count != 0)
        {
            List<string> log = [];
            FullResult authResult = CheckEverything(connectionData, message, log);
            List<MailAttachment> attachments = message.Attachments.Select(x =>
            {
                string? fileName = x.ContentDisposition.FileName?.Trim().HtmlSafe();
                if (fileName == "")
                    fileName = null;
                string? mimeType = x.ContentType.MimeType?.Trim().HtmlSafe();
                if (mimeType == "")
                    mimeType = null;
                return new MailAttachment(fileName, mimeType);
            }).ToList();
            MailMessage mail = new(true, DateTime.UtcNow, message, attachments, authResult, log);
            foreach (string toAddress in mailboxes.Select(x => x.Address))
            {
                var mailboxId = Mailboxes.AddressIndex.Get(toAddress);
                if (mailboxId == null)
                    continue;
                
                List<string> tempFiles = [];
                try
                {
                    Mailboxes.Transaction(mailboxId, (ref Mailbox mailbox, ref List<IFileAction> actions) =>
                    {
                        ulong messageId = (ulong)mail.TimestampUtc.Ticks;
                        while (mailbox.Messages.ContainsKey(messageId))
                            messageId++;
                        mailbox.Messages[messageId] = mail;
                        mailbox.Folders[mailbox.AuthRequirements.SatisfiedBy(authResult) ? "Inbox" : "Spam"].Add(messageId);
                        
                        if (message.TextBody != null)
                            actions.Add(new SetFileAction($"{messageId}/text", path => File.WriteAllText(path, message.TextBody)));
                        if (message.HtmlBody != null)
                            actions.Add(new SetFileAction($"{messageId}/html", path => File.WriteAllText(path, message.HtmlBody)));
                        int attachmentIndex = 0;
                        foreach (var attachment in message.Attachments)
                        {
                            var temp = Path.GetTempFileName();
                            tempFiles.Add(temp);
                            using var stream = File.OpenWrite(temp);
                            switch (attachment)
                            {
                                case MessagePart messagePart:
                                    messagePart.Message.WriteTo(stream);
                                    break;
                                case MimePart mimePart:
                                    mimePart.Content.DecodeTo(stream);
                                    break;
                                default:
                                    Console.WriteLine($"UNRECOGNIZED MAIL ATTACHMENT TYPE {attachment.GetType().FullName}");
                                    break;
                            }
                            stream.Flush();
                            stream.Close();
                            actions.Add(new SetFileAction($"{messageId}/{attachmentIndex}", path => File.Move(temp, path)));
                            attachmentIndex++;
                        }
                    });
                }
                finally
                {
                    foreach (var temp in tempFiles)
                        if (File.Exists(temp))
                            File.Delete(temp);
                }
                if (IncomingListeners.TryGetValue(mailboxId, out var listenerKV))
                    foreach (var listenerKKV in listenerKV)
                        try
                        {
                            listenerKKV.Key.EventMessage(listenerKKV.Value ? "refresh" : "icon").GetAwaiter().GetResult();
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
            Console.WriteLine($"UNRECOGNIZED MAIL (Secure={connectionData.Secure}, Host={connectionData.IP.Address}) '{message.Subject??"[no subject]"}' from {from.Name} ({from.Address}) to {to.Name} ({to.Address})");
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