using MimeKit;
using System.Runtime.Serialization;
using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    [DataContract]
    public class MailAuthResult
    {
        [DataMember]
        public readonly string IPAddress;

        [DataMember]
        public readonly bool Secure;

        [DataMember]
        public readonly MailAuthVerdictSPF SPF;

        [DataMember]
        public readonly MailAuthVerdictDKIM DKIM;

        [DataMember]
        public readonly MailAuthVerdictDMARC DMARC;

        public MailAuthResult(MailConnectionData connectionData, MimeMessage message, List<string> logToPopulate)
        {
            IPAddress = connectionData.IP.Address.ToString();
            Secure = connectionData.Secure;

            logToPopulate.Add("From: " + IPAddress);
            logToPopulate.Add("Secure: " + Secure.ToString());

            SPF = CheckSPF(message.From.Mailboxes.First().Domain, connectionData.IP.Address, out var spfPassedDomain);
            logToPopulate.Add($"SPF: {SPF}{(spfPassedDomain == null ? "" : $" with {spfPassedDomain}")}");

            DKIM = CheckDKIM(message, out var dkimResults);
            logToPopulate.Add($"DKIM: {DKIM}");
            foreach (var ds in dkimResults)
                logToPopulate.Add($"DKIM (domain={ds.Key.Domain}, selector={ds.Key.Selector}): {(ds.Value ? "Pass" : "Fail")}");

            DMARC = MailAuthVerdictDMARC.Unset;
            logToPopulate.Add("DMARC checking was skipped (501).");
        }
    }
}