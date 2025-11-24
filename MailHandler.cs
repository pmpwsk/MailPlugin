using MimeKit;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Protocol;
using uwap.WebFramework.Database;
using uwap.WebFramework.Mail;
using static uwap.WebFramework.Mail.MailAuth;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    private async Task<(bool SendExternally, string? Log)> BeforeSendAsync(MailGen mailGen, MailboxAddress currentRecipient, string potentialMessageId)
    {
        var mailboxId = await Mailboxes.AddressIndex.GetAsync(currentRecipient.Address);
        if (mailboxId == null)
        {
            if (SendMissingInternalRecipientsExternally || !Mailboxes.AddressIndex.Keys.Any(x => x.EndsWith('@' + currentRecipient.Domain)))
                return (true, null);
            else
                return (false, "Address not found.");
        }

        await Mailboxes.TransactionAsync(mailboxId, t =>
        {
            ulong messageId = (ulong)DateTime.UtcNow.Ticks;
            while (t.Value.Messages.ContainsKey(messageId))
                messageId++;
            t.Value.Messages[messageId] = new(true, DateTime.UtcNow, mailGen, potentialMessageId);
            t.Value.Folders["Inbox"].Add(messageId);
            if (mailGen.TextBody != null)
                t.FileActions.Add(new SetFileAction($"{messageId}/text", path => File.WriteAllText(path, mailGen.TextBody)));
            if (mailGen.HtmlBody != null)
                t.FileActions.Add(new SetFileAction($"{messageId}/html", path => File.WriteAllText(path, mailGen.HtmlBody)));
            int attachmentIndex = 0;
            foreach (var attachment in mailGen.Attachments)
            {
                t.FileActions.Add(new SetFileAction($"{messageId}/{attachmentIndex}", path => File.WriteAllBytes(path, attachment.Bytes)));
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
        return (false, "Sent internally.");
    }

    public Task<bool> MailboxExistsAsync(ISessionContext context, IMailbox from, IMailbox to)
    {
        //Option 1: check if any addresses with that host domain exist (so that spammers/attackers can't scan for what mailboxes exist for a given domain)
        return Task.FromResult(Mailboxes.AddressIndex.Keys.Any(x => x.EndsWith('@' + to.Host)));

        //Option 2: check if that exact recipient address exists
        //return Task.FromResult(Mailboxes.AddressIndex.Get(from.AsAddress()) != null);
    }

    public async Task<SmtpResponse> HandleMailAsync(ISessionContext context, MimeMessage message, MailConnectionData connectionData)
    {
        MailboxAddress? from = message.From.Mailboxes.FirstOrDefault();
        if (from == null || !message.To.Mailboxes.Any())
            return SmtpResponse.SyntaxError;
        
        List<MailboxAddress> mailboxes = [];
        foreach (var m in message.To.Mailboxes.Union(message.Cc.Mailboxes).Union(message.Bcc.Mailboxes))
            if (await Mailboxes.AddressIndex.GetAsync(m.Address) != null)
                mailboxes.Add(m);
        
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
                var mailboxId = await Mailboxes.AddressIndex.GetAsync(toAddress);
                if (mailboxId == null)
                    continue;
                
                List<string> tempFiles = [];
                try
                {
                    await Mailboxes.TransactionAsync(mailboxId, t =>
                    {
                        ulong messageId = (ulong)mail.TimestampUtc.Ticks;
                        while (t.Value.Messages.ContainsKey(messageId))
                            messageId++;
                        t.Value.Messages[messageId] = mail;
                        t.Value.Folders[t.Value.AuthRequirements.SatisfiedBy(authResult) ? "Inbox" : "Spam"].Add(messageId);
                        
                        if (message.TextBody != null)
                            t.FileActions.Add(new SetFileAction($"{messageId}/text", path => File.WriteAllText(path, message.TextBody)));
                        if (message.HtmlBody != null)
                            t.FileActions.Add(new SetFileAction($"{messageId}/html", path => File.WriteAllText(path, message.HtmlBody)));
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
                            t.FileActions.Add(new SetFileAction($"{messageId}/{attachmentIndex}", path => File.Move(temp, path)));
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