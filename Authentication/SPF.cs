using System.Net;
using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private MailAuthVerdictSPF CheckSPF(string domain, string sender)
        => CheckSPF(domain, IPAddress.Parse(sender), 0, false);

    private MailAuthVerdictSPF CheckSPF(string domain, IPAddress ip, int depth, bool isInclude)
    {
        try
        {
            if (depth >= 10)
                return MailAuthVerdictSPF.Unset;

            var fields = ResolveTXT(domain, "spf1", new[] { new[] { "+all", "-all", "~all", "?all", "a", "mx", "ip4", "ip6", "redirect", "include" } });
            if (fields == null)
                return MailAuthVerdictSPF.Unset;
            foreach (var field in fields)
                if (field.Key == "redirect" && field.Value != null)
                    return CheckSPF(field.Value, ip, depth + 1, false);

            foreach (var field in fields)
                switch (field.Key)
                {
                    case "+all":
                        if (!isInclude)
                            return MailAuthVerdictSPF.Pass;
                        else return MailAuthVerdictSPF.Unset;
                    case "-all":
                        return MailAuthVerdictSPF.HardFail;
                    case "~all":
                        return MailAuthVerdictSPF.SoftFail;
                    case "?all":
                        return MailAuthVerdictSPF.Unset;
                    case "a":
                        //////////////////////// check a and aaaa! >> multiple results may exist for each one too
                        break;
                    case "mx":
                        ////////////////////////
                        break;
                    case "ip4":
                    case "ip6":
                        if (field.Value != null && IPAddress.TryParse(field.Value, out var fieldIP) && ip.Equals(fieldIP))
                            return MailAuthVerdictSPF.Pass;
                        break;
                    case "include":
                        if (field.Value != null && CheckSPF(field.Value, ip, depth + 1, true) == MailAuthVerdictSPF.Pass)
                            return MailAuthVerdictSPF.Pass;
                        break;

                }

            return MailAuthVerdictSPF.SoftFail;
        }
        catch
        {
            return MailAuthVerdictSPF.Unset;
        }
    }
}