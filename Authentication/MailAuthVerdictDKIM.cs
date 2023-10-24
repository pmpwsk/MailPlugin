namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public enum MailAuthVerdictDKIM
    {
        Fail = -2,
        Mixed = -1,
        Unset = 0,
        Pass = 1
    }
}