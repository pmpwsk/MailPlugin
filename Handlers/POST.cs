using uwap.WebFramework.Accounts;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public override async Task Handle(PostRequest req, string path, string pathPrefix)
    {
        if (!req.LoggedIn)
        {
            req.Status = 403;
            return;
        }

        switch (path)
        {
            case "/save-draft":
                {
                    if (req.IsForm)
                    {
                        req.Status = 400;
                        break;
                    }
                    if (InvalidMailbox(req, out var mailbox))
                        break;
                    if (mailbox.Messages.TryGetValue(0, out var message))
                    {
                        if (message.InReplyToId != null)
                        {
                            mailbox.Lock();
                            goto WriteText;
                        }
                    }
                    else message = null;

                    if (!(req.Query.TryGetValue("to", out var toString) && req.Query.TryGetValue("subject", out var subject)))
                    {
                        req.Status = 400;
                        break;
                    }
                    var to = toString.Split(',', ';', ' ').Where(x => x != "");
                    if (to.Any(x => !AccountManager.CheckMailAddressFormat(x)))
                    {
                        await req.Write("invalid-to");
                        break;
                    }
                    mailbox.Lock();
                    if (message != null)
                    {
                        message.To = to.Select(x => new MailAddress(x, mailbox.Contacts.TryGetValue(x, out var contact) ? contact.Name : x)).ToList();
                        message.Subject = subject.Trim();
                    }
                    else
                    {
                        mailbox.Messages[0] = new(new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address), to.Select(x => new MailAddress(x, mailbox.Contacts.TryGetValue(x, out var contact) ? contact.Name : x)).ToList(), subject.Trim(), null);
                    }

                    WriteText:
                    string text = (await req.GetBodyText());
                    Directory.CreateDirectory($"../Mail/{mailbox.Id}/0");
                    File.WriteAllText($"../Mail/{mailbox.Id}/0/text", text);

                    await req.Write("ok");
                    mailbox.UnlockSave();
                }
                break;
            case "/upload-attachment":
                {
                    if ((!req.IsForm) || req.Files.Count != 1)
                    {
                        req.Status = 400;
                        break;
                    }
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
                        message.Attachments.Add(new(file.FileName?.Trim()?.HtmlSafe(), file.ContentType?.Trim()?.HtmlSafe()));
                    else req.Status = 413;
                    mailbox.UnlockSave();
                }
                break;
            default:
                req.Status = 404;
                break;
        }
    }
}