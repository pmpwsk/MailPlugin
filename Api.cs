using System.Diagnostics.Eventing.Reader;
using System.Web;
using uwap.Database;
using uwap.WebFramework.Accounts;
using uwap.WebFramework.Elements;

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
                    if (InvalidMailbox(req, out var mailbox))
                        break;
                    if (InvalidMessage(req, out var message, out var messageId, mailbox))
                        break;
                    if (InvalidFolder(req, out var folder, out var folderName, messageId))
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
                    if (InvalidMailbox(req, out var mailbox))
                        break;
                    if (!req.Query.TryGetValue("message", out var messageIdString))
                    {
                        req.Status = 400;
                        break;
                    }
                    if ((!ulong.TryParse(messageIdString, out ulong messageId)) || !mailbox.Messages.TryGetValue(messageId, out var message))
                    {
                        req.Status = 404;
                        break;
                    }
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
            default:
                req.Status = 404;
                break;
        }
    }
}