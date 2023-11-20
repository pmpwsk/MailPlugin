namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public override Task Handle(UploadRequest req, string path, string pathPrefix)
    {
        if (!req.LoggedIn)
        {
            req.Status = 403;
            return Task.CompletedTask;
        }

        switch (path)
        {
            case "/upload-attachment":
                {
                    if (InvalidMailbox(req, out var mailbox))
                        break;
                    mailbox.Lock();
                    if (mailbox.Messages.TryGetValue(0, out var message))
                    {
                        message.From = new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address);
                    }
                    else
                    {
                        message = new(new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address), [], "", null);
                        mailbox.Messages[0] = message;
                    }
                    Directory.CreateDirectory($"../Mail/{mailbox.Id}/0");
                    var file = req.Files[0];
                    int attachmentId = message.Attachments.Count;
                    if (file.Download($"../Mail/{mailbox.Id}/0/{attachmentId}", 10485760))
                        message.Attachments.Add(new(file.FileName, file.ContentType));
                    else req.Status = 413;
                    mailbox.UnlockSave();
                }
                break;
            default:
                req.Status = 404;
                break;
        }

        return Task.CompletedTask;
    }
}