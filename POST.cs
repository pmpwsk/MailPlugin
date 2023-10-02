using uwap.WebFramework.Accounts;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public override async Task Handle(PostRequest req, string path, string pathPrefix)
    {
        if (req.User == null || (!req.LoggedIn))
        {
            req.Status = 403;
            return;
        }

        switch (path)
        {
            case "/save-draft":
                {
                    if (InvalidMailbox(req, out var mailbox))
                        break;
                    if (!(req.Query.TryGetValue("to", out var toString) && req.Query.TryGetValue("subject", out var subject)))
                    {
                        req.Status = 400;
                        break;
                    }
                    var to = toString.Split(',', ';', ' ');
                    if (to.Any(x => !AccountManager.CheckMailAddressFormat(x)))
                    {
                        await req.Write("invalid-to");
                        break;
                    }
                    string text = await req.GetBodyText();
                    Directory.CreateDirectory($"../Mail/{mailbox.Id}/0");
                    File.WriteAllText($"../Mail/{mailbox.Id}/0/text", text);
                    mailbox.Lock();
                    if (mailbox.Messages.TryGetValue(0, out var message))
                    {
                        message.From = new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address);
                        message.To = to.Select(x => new MailAddress(x, x)).ToList();
                        message.Subject = subject;
                    }
                    else
                    {
                        mailbox.Messages[0] = new(new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address), to.Select(x => new MailAddress(x, x)).ToList(), subject, null);
                    }
                    mailbox.UnlockSave();
                }
                break;
            default:
                req.Status = 404;
                break;
        }
    }
}