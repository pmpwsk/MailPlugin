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
                    await req.SendFile(filePath, attachment.Name);
                }
                break;
            default:
                req.Status = 404;
                break;
        }
    }
}