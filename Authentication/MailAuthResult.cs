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

            string fromDomain = message.From.Mailboxes.First().Domain;
            string? returnHeader = message.Headers[HeaderId.ReturnPath];
            string returnDomain = (returnHeader != null && MailboxAddress.TryParse(returnHeader, out var address)) ? address.Domain : fromDomain;

            SPF = CheckSPF(returnDomain, connectionData.IP.Address, out var spfPassedDomain);
            logToPopulate.Add($"SPF: {SPF}{(spfPassedDomain == null ? "" : $" with {spfPassedDomain}")}");

            DKIM = CheckDKIM(message, out var dkimResults);
            logToPopulate.Add($"DKIM: {DKIM}");
            foreach (var ds in dkimResults)
                logToPopulate.Add($"DKIM (domain={ds.Key.Domain}, selector={ds.Key.Selector}): {(ds.Value ? "Pass" : "Fail")}");

            DMARC = CheckDMARC(returnDomain, fromDomain, SPF, DKIM, dkimResults);
            logToPopulate.Add($"DMARC: {DMARC}");
        }
    }
}