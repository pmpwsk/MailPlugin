using MimeKit;
using uwap.Database;
using uwap.WebFramework.Accounts;
using uwap.WebFramework.Elements;
using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public async Task HandleForward(Request req)
    {
        switch (req.Path)
        {
            // FORWARD MAIL
            case "/forward":
            { CreatePage(req, "Forward", out var page, out var e);
                if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out var message, out var messageId, out _, out var folderName))
                    break;
                page.Navigation.Add(new Button("Back", $".?mailbox={mailbox.Id}&folder={folderName}&message={messageId}", "right"));
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("query.js"));
                page.Scripts.Add(new Script("forward.js"));
                e.Add(new LargeContainerElement("Forward", message.Subject));
                page.AddError();
                e.Add(new ContainerElement("Option 1: Quote",
                [
                    new Paragraph("Quotes this email in a new draft."),
                    new Checkbox("Include the entire conversation (not just the last 1-2 messages)", "everything")
                ]) { Button = new ButtonJS("Draft", "Quote()", "green") });
                e.Add(new ContainerElement("Option 2: Original",
                [
                    new Paragraph("Sends the exact message."),
                    new Checkbox("Add information about the original message", "info", true),
                    new TextBox("Recipient(s)...", null, "to", TextBoxRole.Email, "Original()", autofocus: true)
                ]) { Button = new ButtonJS("Send", "Original()", "green", id: "send")});
            } break;

            case "/forward/quote":
            { POST(req);
                if (InvalidMailboxOrMessageOrFolder(req, out var readMailbox, out var message, out var messageId, out _, out _))
                    break;
                if (!(req.Query.TryGetValue("everything", out string? everythingS) && bool.TryParse(everythingS, out bool everything)))
                    throw new BadRequestSignal();
                Mailboxes.TransactionIgnoreNull(readMailbox.Id, (ref Mailbox mailbox, ref List<IFileAction> fileActions) =>
                {
                    string? text = mailbox.GetFileText($"{messageId}/html") ?? mailbox.GetFileText($"{messageId}/text")?.Map(t => AddHTML(t.HtmlSafe()));
                    text = text != null ? $"\n\n\n# Forwarded message:\n# From: {message.From.FullString}\n# Time: {DateTimeString(message.TimestampUtc)} UTC\n\n\n{QuoteHTML(everything ? text : Before(text, "# Original message:").TrimEnd())}" : "";

                    if (mailbox.Footer != null)
                        text = "\n\n" + mailbox.Footer + text;

                    string subject = message.Subject.Trim();
                    while (subject.SplitAtFirst(':', out var subjectPrefix, out var realSubject) && subjectPrefix.All(char.IsLetter) && (subjectPrefix.Length == 2 || subjectPrefix.Length == 3) && realSubject.TrimStart() != "")
                        subject = realSubject.TrimStart();
                    mailbox.Messages[0] = new(new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address), [], "Fwd: " + subject, null);
                    fileActions.Add(new SetFileAction("0/text", path => File.WriteAllText(path, text)));
                });
            } break;

            case "/forward/original":
            { POST(req);
                if (InvalidMailboxOrMessageOrFolder(req, out var readMailbox, out var originalMessage, out var originalMessageId, out _, out _))
                    break;
                if (!(req.Query.TryGetValue("info", out string? infoS) && bool.TryParse(infoS, out bool info) && req.Query.TryGetValue("to", out var toString)))
                    throw new BadRequestSignal();
                var to = toString.Split(',', ';', ' ').Where(x => x != "").ToList();
                if (to.Any(x => !AccountManager.CheckMailAddressFormat(x)))
                {
                    await req.Write("invalid-to");
                    break;
                }
                var messageId = Mailboxes.TransactionNullableAndGet(readMailbox.Id, (ref Mailbox? mailbox, ref List<IFileAction> fileActions) =>
                {
                    if (mailbox == null)
                        return 0UL;
                    
                    string subject = originalMessage.Subject.Trim();
                    while (subject.SplitAtFirst(':', out var subjectPrefix, out var realSubject) && subjectPrefix.All(char.IsLetter) && (subjectPrefix.Length == 2 || subjectPrefix.Length == 3) && realSubject.TrimStart() != "")
                        subject = realSubject.TrimStart();
                    MailMessage message = new(new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address), to.Select(x => new MailAddress(x, readMailbox.Contacts.TryGetValue(x, out var contact) ? contact.Name : x)).ToList(), "Fwd: " + subject, null);
                    foreach (var attachment in originalMessage.Attachments)
                        message.Attachments.Add(attachment);
                    string? htmlPart = mailbox.GetFileText($"{originalMessageId}/html");
                    string? textPart = mailbox.GetFileText($"{originalMessageId}/text");
                    if (info)
                    {
                        string prefix = $"# Forwarded message:\n# From: {originalMessage.From.FullString}\n# Time: {DateTimeString(originalMessage.TimestampUtc)} UTC\n\n\n";
                        if (htmlPart != null)
                            htmlPart = prefix.Replace("\n", "<br/>") + htmlPart;
                        if (textPart != null)
                            textPart = prefix + textPart;
                    }
                    MailGen msg = new(new(message.From.Name, message.From.Address), message.To.Select(x => new MailboxAddress(x.Name, x.Address)), message.Subject, textPart, htmlPart);
                    int counter = 0;
                    foreach (var attachment in originalMessage.Attachments)
                    {
                        msg.Attachments.Add(new(string.IsNullOrEmpty(attachment.Name) ? "Unknown name" : attachment.Name, attachment.MimeType, mailbox.GetFileBytes($"{originalMessageId}/{counter}") ?? []));
                        counter++;
                    }
                    var result = MailManager.Out.Send(msg, out var messageIds);
                    message.MessageId = string.Join('\n', messageIds);
                    var log = message.Log;
                    if (result.Internal.Count != 0)
                    {
                        log.Add("Internal:");
                        foreach (var l in result.Internal)
                            log.Add(l.Key.Address + ": " + l.Value);
                    }
                    if (result.FromSelf != null)
                    {
                        log.Add("From the server directly: " + result.FromSelf.ResultType);
                        foreach (string l in result.FromSelf.ConnectionLog)
                            log.Add(l);
                    }
                    if (result.FromBackup != null)
                    {
                        log.Add("From the backup sender: " + result.FromBackup.ResultType);
                        foreach (string l in result.FromBackup.ConnectionLog)
                            log.Add(l);
                    }
                    ulong messageId = (ulong)message.TimestampUtc.Ticks;
                    while (mailbox.Messages.ContainsKey(messageId))
                        messageId++;
                    mailbox.Messages[messageId] = message;
                    mailbox.Folders["Sent"].Add(messageId);
                    for (int i = 0; i < originalMessage.Attachments.Count; i++)
                        if (mailbox.TryGetFilePath($"{originalMessageId}/{i}", out var filePath))
                            fileActions.Add(new SetFileAction($"{messageId}/{i}", targetPath => File.Copy(filePath, targetPath)));
                    if (htmlPart != null)
                        fileActions.Add(new SetFileAction($"{messageId}/html", targetPath => File.WriteAllText(targetPath, htmlPart)));
                    if (textPart != null)
                        fileActions.Add(new SetFileAction($"{messageId}/text", targetPath => File.WriteAllText(targetPath, textPart)));
                    
                    return messageId;
                });
                if (messageId != 0)
                    await req.Write("message=" + messageId);
            } break;
            



            // 404
            default:
                req.CreatePage("Error");
                req.Status = 404;
                break;
        }
    }
}