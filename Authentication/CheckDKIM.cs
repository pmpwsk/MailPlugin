using MimeKit;
using MimeKit.Cryptography;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private static MailAuthVerdictDKIM CheckDKIM(MimeMessage message, out Dictionary<string,bool> domainResults)
    {
        try
        {
            domainResults = new();
            var result = MailAuthVerdictDKIM.Unset;



            return result;
        }
        catch
        {
            domainResults = new();
            return MailAuthVerdictDKIM.Unset;
        }
    }
}