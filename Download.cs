using Org.BouncyCastle.Ocsp;
using System.Text;
using uwap.WebFramework.Accounts;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public override async Task Handle(DownloadRequest req, string path, string pathPrefix)
    {
        if (req.User == null || (!req.LoggedIn))
        {
            req.Status = 403;
            return;
        }

        switch (path)
        {
            case "/attachment":
                {
                    if (!req.Query.TryGetValue("mailbox", out string? mailboxId))
                    {
                        req.Status = 400;
                        break;
                    }
                    if (!Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                    {
                        req.Status = 404;
                        break;
                    }
                    if ((!mailbox.AllowedUserIds.TryGetValue(req.UserTable.Name, out var allowedUserIds)) || !allowedUserIds.Contains(req.User.Id))
                    {
                        req.Status = 403;
                        break;
                    }
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
                    if (attachmentId < 0 || attachmentId >= message.Attachments.Length)
                    {
                        req.Status = 404;
                        break;
                    }
                    string filePath = $"../Mail/{mailboxId}/{messageId}/{attachmentId}";
                    if (!File.Exists(filePath))
                    {
                        req.Status = 404;
                        break;
                    }
                    MailAttachment attachment = message.Attachments[attachmentId];
                    req.Context.Response.ContentType = attachment.MimeType;
                    await req.SendFile(filePath, attachment.Name);
                }
                break;
            default:
                req.Status = 404;
                break;
        }
    }
}