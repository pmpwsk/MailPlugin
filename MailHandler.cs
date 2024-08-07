﻿using MimeKit;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Protocol;
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
        Directory.CreateDirectory($"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}");
        if (mailGen.TextBody != null)
            File.WriteAllText($"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}/text", mailGen.TextBody);
        if (mailGen.HtmlBody != null)
            File.WriteAllText($"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}/html", mailGen.HtmlBody);
        int attachmentIndex = 0;
        foreach (var attachment in mailGen.Attachments)
        {
            File.WriteAllBytes($"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}/{attachmentIndex}", attachment.Bytes);
            attachmentIndex++;
        }
        if (IncomingListeners.TryGetValue(mailbox, out var listenerKV))
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
        return Mailboxes.MailboxByAddress.Keys.Any(x => x.EndsWith('@' + to.Host));

        //Option 2: check if that exact recipient address exists
        //return Mailboxes.MailboxByAddress.ContainsKey(from.AsAddress());
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
            List<MailAttachment> attachments = message.Attachments.Select(x =>
            {
                string? fileName = x.ContentDisposition.FileName?.Trim()?.HtmlSafe();
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
                Mailbox mailbox = Mailboxes.MailboxByAddress[toAddress];
                mailbox.Lock();
                ulong messageId = (ulong)mail.TimestampUtc.Ticks;
                while (mailbox.Messages.ContainsKey(messageId))
                    messageId++;
                mailbox.Messages[messageId] = mail;
                mailbox.Folders[mailbox.AuthRequirements.SatisfiedBy(authResult) ? "Inbox" : "Spam"].Add(messageId);
                mailbox.UnlockSave();
                Directory.CreateDirectory($"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}");
                if (message.TextBody != null)
                    File.WriteAllText($"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}/text", message.TextBody);
                if (message.HtmlBody != null)
                    File.WriteAllText($"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}/html", message.HtmlBody);
                int attachmentIndex = 0;
                foreach (var attachment in message.Attachments)
                {
                    using var stream = File.OpenWrite($"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}/{attachmentIndex}");
                    if (attachment is MessagePart messagePart)
                        messagePart.Message.WriteTo(stream);
                    else if (attachment is MimePart mimePart)
                        mimePart.Content.DecodeTo(stream);
                    else Console.WriteLine($"UNRECOGNIZED MAIL ATTACHMENT TYPE {attachment.GetType().FullName}");
                    stream.Flush();
                    stream.Close();
                    stream.Dispose();
                    attachmentIndex++;
                }
                if (IncomingListeners.TryGetValue(mailbox, out var listenerKV))
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