namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private enum DmarcPolicy
    {
        None,
        Quarantine,
        Reject
    }
}