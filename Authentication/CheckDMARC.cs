namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private static bool GetDmarcSettings(string domain, bool preferSubdomain, out DmarcPolicy policy, out DmarcAlignment alignmentSPF, out DmarcAlignment alignmentDKIM)
    {
        var fields = ResolveTXT($"_dmarc.{domain}", null, new[] { new[] { "p" } });
        if (fields != null)
        {
            DmarcPolicy p = DmarcPolicy.Quarantine;
            DmarcPolicy? sp = null;
            alignmentSPF = DmarcAlignment.Relaxed;
            alignmentDKIM = DmarcAlignment.Relaxed;
            foreach (var field in fields)
            {
                switch (field.Key)
                {
                    case "p":
                    case "sp":
                        DmarcPolicy newPolicy;
                        switch (field.Value)
                        {
                            case "none":
                            case "n":
                                newPolicy = DmarcPolicy.None;
                                break;
                            case "quarantine":
                            case "q":
                                newPolicy = DmarcPolicy.Quarantine;
                                break;
                            case "reject":
                            case "r":
                                newPolicy = DmarcPolicy.Reject;
                                break;
                            default:
                                goto invalid;
                        }
                        if (field.Key == "p")
                            p = newPolicy;
                        else sp = newPolicy;
                        break;
                    case "aspf":
                    case "adkim":
                        DmarcAlignment newAlignment;
                        switch (field.Value)
                        {
                            case "relaxed":
                            case "r":
                                newAlignment = DmarcAlignment.Relaxed;
                                break;
                            case "strict":
                            case "s":
                                newAlignment = DmarcAlignment.Strict;
                                break;
                            default:
                                goto invalid;
                        }
                        if (field.Key == "aspf")
                            alignmentSPF = newAlignment;
                        else alignmentDKIM = newAlignment;
                        break;
                }
            }
            policy = (preferSubdomain && sp != null) ? sp.Value : p;
            return true;
        }
        else if (domain.SplitAtFirst('.', out var _, out var newDomain) && newDomain.Contains('.'))
            return GetDmarcSettings(newDomain, true, out policy, out alignmentSPF, out alignmentDKIM);

        invalid:
        policy = DmarcPolicy.Quarantine;
        alignmentSPF = DmarcAlignment.Relaxed;
        alignmentDKIM = DmarcAlignment.Relaxed;
        return false;
    }

    private static MailAuthVerdictDMARC ToVerdict(DmarcPolicy policy)
        => policy switch
        {
            DmarcPolicy.Quarantine => MailAuthVerdictDMARC.FailWithQuarantine,
            DmarcPolicy.Reject => MailAuthVerdictDMARC.FailWithReject,
            DmarcPolicy.None => MailAuthVerdictDMARC.FailWithoutAction,
            _ => throw new Exception("The given policy wasn't recognized."),
        };
}