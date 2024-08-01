using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public MailPlugin(bool assignEvents = true)
    {
        Directory.CreateDirectory("../MailPlugin.Mailboxes");

        if (assignEvents)
        {
            MailManager.In.MailboxExists += MailboxExists;
            MailManager.In.HandleMail += HandleMail;
            MailManager.Out.BeforeSend += BeforeSend;
        }
    }
}