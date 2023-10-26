﻿using MimeKit;
using MimeKit.Cryptography;

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
                string? domain = null, selector = null;
                try
                {
                    var parameters = MimeKitCryptographyUser.ParseParameterTags(header.Id, header.Value);
                    MimeKitCryptographyUser.ValidateDkimSignatureParameters(parameters, out _, out _, out _, out domain, out selector, out _, out _, out _, out _, out _);
                }
                catch
                {
                    continue;
                }

                try
                {
                    if (domain == null || selector == null)
                        continue;
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
}