using Microsoft.AspNetCore.Server.HttpSys;
using MimeKit;
using System.Diagnostics.Eventing.Reader;
using System.Web;
using uwap.Database;
using uwap.WebFramework.Accounts;
using uwap.WebFramework.Elements;
using uwap.WebFramework.Mail;
using static QRCoder.PayloadGenerator;
using static System.Net.Mime.MediaTypeNames;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public override async Task Handle(ApiRequest req, string path, string pathPrefix)
    {
        if (req.User == null || (!req.LoggedIn))
        {
            req.Status = 403;
            return;
        }

        switch (path)
        {
            case "/create-mailbox":
                if (!req.IsAdmin())
                    req.Status = 403;
                else
                {
                    if (!req.Query.TryGetValue("address", out string? address))
                    {
                        req.Status = 400;
                    }
                    else if (!AccountManager.CheckMailAddressFormat(address))
                    {
                        await req.Write("format");
                    }
                    else if (Mailboxes.MailboxByAddress.ContainsKey(address))
                    {
                        await req.Write("exists");
                    }
                    else
                    {
                        string id;
                        do id = Parsers.RandomString(10);
                        while (Mailboxes.ContainsKey(id));
                        Mailbox mailbox = new(id, address);
                        Mailboxes[id] = mailbox;
                        MailboxTable.AddToAccelerators(mailbox, Mailboxes.UserAllowedMailboxes, Mailboxes.MailboxByAddress);
                        Directory.CreateDirectory($"../Mail/{mailbox.Id}");
                        await req.Write(pathPrefix + "/manage?mailbox=" + id);
                    }
                }
                break;
            case "/delete-mailbox":
                if (!req.IsAdmin())
                    req.Status = 403;
                else
                {
                    if (!req.Query.TryGetValue("mailbox", out string? mailboxId))
                    {
                        req.Status = 400;
                    }
                    else if (Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                    {
                        Mailboxes.Delete(mailboxId);
                        MailboxTable.RemoveFromAccelerators(mailbox, Mailboxes.UserAllowedMailboxes, Mailboxes.MailboxByAddress);
                        if (Directory.Exists($"../Mail/{mailbox.Id}"))
                            Directory.Delete($"../Mail/{mailbox.Id}", true);
                        await req.Write("ok");
                    }
                    else await req.Write("missing");
                }
                break;
            case "/add-access":
                if (!req.IsAdmin())
                    req.Status = 403;
                else
                {
                    if ((!req.Query.TryGetValue("mailbox", out var mailboxId)) || !req.Query.TryGetValue("username", out var usernameCombined))
                    {
                        req.Status = 400;
                        break;
                    }
                    UserTable? userTable = req.UserTable;
                    string username = usernameCombined;
                    int colon = usernameCombined.IndexOf(':');
                    if (colon != -1)
                    {
                        userTable = AccountManager.Settings.UserTables.Values.FirstOrDefault(x => x.Name == usernameCombined.Remove(colon));
                        username = usernameCombined.Remove(0, colon + 1);
                    }
                    if (userTable == null)
                    {
                        await req.Write("invalid");
                        break;
                    }
                    User? user = userTable.FindByUsername(username);
                    if (user == null)
                    {
                        await req.Write("invalid");
                        break;
                    }
                    if (Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                    {
                        mailbox.Lock();
                        if (!mailbox.AllowedUserIds.TryGetValue(userTable.Name, out var userTableDict))
                        {
                            userTableDict = new();
                            mailbox.AllowedUserIds[userTable.Name] = userTableDict;
                        }
                        userTableDict.Add(user.Id);
                        if (!Mailboxes.UserAllowedMailboxes.TryGetValue(userTable.Name, out var userTableDict2))
                        {
                            userTableDict2 = new();
                            Mailboxes.UserAllowedMailboxes[userTable.Name] = userTableDict2;
                        }
                        if (!userTableDict2.TryGetValue(user.Id, out var userSet))
                        {
                            userSet = new();
                            userTableDict2[user.Id] = userSet;
                        }
                        userSet.Add(mailbox);
                        mailbox.UnlockSave();
                        await req.Write("ok");
                    }
                    else await req.Write("missing");
                }
                break;
            case "/remove-access":
                if (!req.IsAdmin())
                    req.Status = 403;
                else
                {
                    if ((!req.Query.TryGetValue("mailbox", out var mailboxId)) || !req.Query.TryGetValue("id", out var idCombined))
                    {
                        req.Status = 400;
                        break;
                    }
                    int colon = idCombined.IndexOf(':');
                    if (colon == -1)
                    {
                        req.Status = 400;
                        break;
                    }
                    string userTableName = idCombined.Remove(colon);
                    string userId = idCombined.Remove(0, colon + 1);
                    if (Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                    {
                        mailbox.Lock();
                        if (mailbox.AllowedUserIds.TryGetValue(userTableName, out var userTableSet))
                        {
                            userTableSet.Remove(userId);
                            if (!userTableSet.Any())
                                mailbox.AllowedUserIds.Remove(userTableName);
                        }
                        if (Mailboxes.UserAllowedMailboxes.TryGetValue(userTableName, out var userTableDict))
                        {
                            if (userTableDict.TryGetValue(userId, out var userSet))
                            {
                                userSet.Remove(mailbox);
                                if (!userSet.Any())
                                    userTableDict.Remove(userId);
                            }
                            if (!userTableDict.Any())
                                Mailboxes.UserAllowedMailboxes.Remove(userTableName);
                        }
                        mailbox.UnlockSave();
                    }
                    await req.Write("ok");
                }
                break;
            case "/delete-message":
                {
                    if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out _, out var messageId, out var folder, out _))
                        break;
                    mailbox.Lock();
                    string messagePath = $"../Mail/{mailbox.Id}/{messageId}";
                    if (Directory.Exists(messagePath))
                        Directory.Delete(messagePath, true);
                    mailbox.Messages.Remove(messageId);
                    folder.Remove(messageId);
                    mailbox.UnlockSave();
                    await req.Write("ok");
                }
                break;
            case "/attachment":
                {
                    if (InvalidMailboxOrMessage(req, out var mailbox, out var message, out var messageId))
                        break;
                    if ((!req.Query.TryGetValue("attachment", out string? attachmentIdString)) || !int.TryParse(attachmentIdString, out var attachmentId))
                    {
                        req.Status = 400;
                        break;
                    }
                    if (attachmentId < 0 || attachmentId >= message.Attachments.Count)
                    {
                        req.Status = 404;
                        break;
                    }
                    string filePath = $"../Mail/{mailbox.Id}/{messageId}/{attachmentId}";
                    if (!File.Exists(filePath))
                    {
                        req.Status = 404;
                        break;
                    }
                    MailAttachment attachment = message.Attachments[attachmentId];
                    req.Context.Response.ContentType = attachment.MimeType;
                    await req.SendFile(filePath);
                }
                break;
            case "/delete-draft":
                {
                    if (InvalidMailbox(req, out var mailbox))
                        break;
                    if (!mailbox.Messages.ContainsKey(0))
                        break;
                    mailbox.Lock();
                    mailbox.Messages.Remove(0);
                    if (Directory.Exists($"../Mail/{mailbox.Id}/0"))
                        Directory.Delete($"../Mail/{mailbox.Id}/0", true);
                    mailbox.UnlockSave();
                }
                break;
            case "/send-draft":
                {
                    if (InvalidMailbox(req, out var mailbox))
                        break;
                    if (!mailbox.Messages.TryGetValue(0, out var message))
                    {
                        await req.Write("no-draft");
                        break;
                    }
                    if (message.Subject == "")
                    {
                        await req.Write("invalid-subject");
                        break;
                    }
                    if (!message.To.Any())
                    {
                        await req.Write("invalid-to");
                        break;
                    }
                    string text = File.Exists($"../Mail/{mailbox.Id}/0/text") ? File.ReadAllText($"../Mail/{mailbox.Id}/0/text") : "";
                    if (text == "")
                    {
                        await req.Write("invalid-text");
                        break;
                    }
                    mailbox.Lock();
                    message.TimestampUtc = DateTime.UtcNow;
                    message.From = new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address);
                    MailGen msg = new(new(message.From.Name, message.From.Address), message.To.Select(x => new MailboxAddress(x.Name, x.Address)), message.Subject, text, null);
                    if (message.InReplyToId != null)
                        msg.IsReplyToMessageId = message.InReplyToId;
                    int counter = 0;
                    foreach (var attachment in message.Attachments)
                    {
                        msg.Attachments.Add(new($"../Mail/{mailbox.Id}/0/{counter}", attachment.Name ?? "missing file name", attachment.MimeType));
                        counter++;
                    }
                    var result = MailManager.Out.Send(msg, out var messageIds);
                    message.MessageId = string.Join('\n', messageIds);
                    var log = message.Log;
                    if (result.FromSelf != null)
                    {
                        log.Add("From the server directly: " + result.FromSelf.ResultType.ToString());
                        foreach (string l in result.FromSelf.ConnectionLog)
                            log.Add(l);
                    }
                    if (result.FromBackup != null)
                    {
                        log.Add("From the backup sender: " + result.FromBackup.ResultType.ToString());
                        foreach (string l in result.FromBackup.ConnectionLog)
                            log.Add(l);
                    }
                    mailbox.Messages.Remove(0);
                    ulong messageId = (ulong)message.TimestampUtc.Ticks;
                    while (mailbox.Messages.ContainsKey(messageId))
                        messageId++;
                    mailbox.Messages[messageId] = message;
                    mailbox.Folders["Sent"].Add(messageId);
                    Directory.Move($"../Mail/{mailbox.Id}/0", $"../Mail/{mailbox.Id}/{messageId}");
                    await req.Write("message=" + messageId);
                    mailbox.UnlockSave();
                }
                break;
            case "/delete-attachment":
                {
                    if (InvalidMailbox(req, out var mailbox))
                        break;
                    if (!req.Query.TryGetValue("attachment", out var attachmentId))
                    {
                        req.Status = 400;
                        break;
                    }
                    if (!int.TryParse(attachmentId, out var a))
                    {
                        req.Status = 400;
                        break;
                    }
                    if (a < 0)
                    {
                        req.Status = 400;
                        break;
                    }
                    if (!mailbox.Messages.TryGetValue(0, out var message))
                    {
                        req.Status = 404;
                        break;
                    }
                    if (a >= message.Attachments.Count)
                    {
                        req.Status = 404;
                        break;
                    }
                    mailbox.Lock();
                    message.Attachments.RemoveAt(a);
                    File.Delete($"../Mail/{mailbox.Id}/0/{a}");
                    a++;
                    while (File.Exists($"../Mail/{mailbox.Id}/0/{a}"))
                    {
                        File.Move($"../Mail/{mailbox.Id}/0/{a}", $"../Mail/{mailbox.Id}/0/{a - 1}");
                        a++;
                    }
                    mailbox.UnlockSave();
                }
                break;
            case "/unread":
                {
                    if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out var message, out _, out _, out var folderName))
                        break;
                    if (folderName == "Sent")
                    {
                        req.Status = 400;
                        break;
                    }
                    mailbox.Lock();
                    message.Unread = true;
                    mailbox.UnlockSave();
                }
                break;
            case "/reply":
                {
                    if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out var message, out _, out _, out var folderName))
                        break;
                    if (folderName == "Sent")
                    {
                        req.Status = 400;
                        break;
                    }
                    mailbox.Lock();
                    mailbox.Messages[0] = new(new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address), new() { message.From }, (message.Subject.ToLower().StartsWith("re:") ? "" : "Re: ") + message.Subject, message.MessageId);
                    Directory.CreateDirectory($"../Mail/{mailbox.Id}/0");
                    File.WriteAllText($"../Mail/{mailbox.Id}/0/text", "");
                    mailbox.UnlockSave();
                }
                break;
            default:
                req.Status = 404;
                break;
        }
    }
}