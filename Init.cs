using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public MailPlugin(bool assignEvents = true)
    {
        Directory.CreateDirectory("../Mail");

        if (assignEvents)
        {
            MailManager.In.AcceptMail += AcceptMail;
            MailManager.In.HandleMail += HandleMail;
        }
    }
}