using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public MailPlugin(bool assignEvents = true)
    {
        if (assignEvents)
        {
            AssignEvents().GetAwaiter().GetResult();
        }
    }
    
    private async Task AssignEvents()
    {
        await MailManager.In.MailboxExists.RegisterAsync(MailboxExistsAsync);
        await MailManager.In.HandleMail.RegisterAsync(HandleMailAsync);
        await MailManager.Out.BeforeSend.RegisterAsync(BeforeSendAsync);
    }
}