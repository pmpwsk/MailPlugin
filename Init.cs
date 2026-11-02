using uwap.WebFramework.Database;
using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public MailPlugin(List<ClusterNode> clusterNodes, bool assignEvents = true)
    {
        Mailboxes = new("MailPlugin.Mailboxes", clusterNodes);
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