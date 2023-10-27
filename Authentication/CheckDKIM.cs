using MimeKit;
using MimeKit.Cryptography;
using System.Diagnostics.CodeAnalysis;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private static MailAuthVerdictDKIM CheckDKIM(MimeMessage message, out Dictionary<DomainSelectorPair,bool> domainResults)
    {
        try
        {
            domainResults = new();
            var result = MailAuthVerdictDKIM.Unset;

            var verifier = new DkimVerifier(new DkimPublicKeyLocator());

            foreach (var header in message.Headers.Where(x => x.Id == HeaderId.DkimSignature))
            {
                if (!GetDomainAndSelector(header.Value, out string? domain, out string? selector))
                    continue;

                try
                {
                    bool valid = verifier.Verify(message, header);
                    domainResults[new(domain, selector)] = valid;
                    switch (result)
                    {
                        case MailAuthVerdictDKIM.Unset:
                            result = valid ? MailAuthVerdictDKIM.Pass : MailAuthVerdictDKIM.Fail;
                            break;
                        case MailAuthVerdictDKIM.Pass:
                            if (!valid)
                                result = MailAuthVerdictDKIM.Mixed;
                            break;
                        case MailAuthVerdictDKIM.Fail:
                            if (valid)
                                result = MailAuthVerdictDKIM.Mixed;
                            break;
                    }
                }
                catch
                {
                    if (domain != null && selector != null)
                    {
                        domainResults[new(domain, selector)] = false;
                        switch (result)
                        {
                            case MailAuthVerdictDKIM.Unset:
                                result = MailAuthVerdictDKIM.Fail;
                                break;
                            case MailAuthVerdictDKIM.Pass:
                                result = MailAuthVerdictDKIM.Mixed;
                                break;
                        }
                    }
                }
            }

            return result;
        }
        catch
        {
            domainResults = new();
            return MailAuthVerdictDKIM.Unset;
        }
    }

    public static bool GetDomainAndSelector(string dkimSignature, [MaybeNullWhen(false)] out string domain, [MaybeNullWhen(false)] out string selector)
    {
        domain = null;
        selector = null;
        foreach (string item in dkimSignature.Split(';'))
        {
            if (item.SplitAtFirst('=', out string? k, out string? v))
            {
                string key = k.Trim();
                string value = v.Trim();
                switch(key)
                {
                    case "d":
                        if (value != "")
                            domain = value;
                        break;
                    case "s":
                        if (selector != "")
                            selector = value;
                        break;
                }
            }
        }
        return domain != null && selector != null;
    }
}