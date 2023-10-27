namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public enum MailAuthVerdictDMARC
    {
        FailWithReject = -3,
        FailWithQuarantine = -2,
        FailWithoutAction = -1,
        Unset = 0,
        Pass = 1
    }
}