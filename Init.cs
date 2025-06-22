using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public MailPlugin(bool assignEvents = true)
    {
        Directory.CreateDirectory("../MailPlugin.Mailboxes");

        if (assignEvents)
        {
            MailManager.In.MailboxExists.Register(MailboxExists);
            MailManager.In.HandleMail.Register(HandleMail);
            MailManager.Out.BeforeSend.Register(BeforeSend);
        }
    }
}