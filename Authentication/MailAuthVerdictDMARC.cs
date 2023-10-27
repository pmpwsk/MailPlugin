namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public enum MailAuthVerdictDMARC
    {
        Reject = -3,
        Quarantine = -2,
        NotAligned = -1,
        Unset = 0,
        Pass = 1
    }
}