using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public MailPlugin(bool assignEvents = true)
    {
        if (assignEvents)
        {
            MailManager.In.MailboxExists.Register(MailboxExists);
            MailManager.In.HandleMail.Register(HandleMail);
            MailManager.Out.BeforeSend.Register(BeforeSend);
        }
    }
}