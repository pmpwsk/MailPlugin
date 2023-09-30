namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public override Task Work()
    {
        Mailboxes.RebuildAccelerators();
        return Task.CompletedTask;
    }
}