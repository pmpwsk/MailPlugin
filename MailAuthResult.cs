using MimeKit;
using SmtpServer;
using System.Net;
using System.Runtime.Serialization;

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
        public readonly MailAuthVerdict SPF;

        [DataMember]
        public readonly MailAuthVerdict DKIM;

        [DataMember]
        public readonly MailAuthVerdict DMARC;

        public MailAuthResult(ISessionContext context, MimeMessage message, List<string> logToPopulate)
        {
            IPAddress = ((IPEndPoint)context.Properties["EndpointListener:RemoteEndPoint"]).Address.ToString();
            Secure = context.Pipe.IsSecure;

            SPF = MailAuthVerdict.Unset;
            DKIM = MailAuthVerdict.Unset;
            DMARC = MailAuthVerdict.Unset;
        }

        public MailAuthResult(Mail.MailConnectionData oldResult, MimeMessage message, List<string> logToPopulate) //replace oldResult with the real parameters when moving this to WF!
        {
            IPAddress = oldResult.IP.Address.ToString();
            Secure = oldResult.Secure;

            SPF = MailAuthVerdict.Unset;
            logToPopulate.Add("SPF checking was skipped (501).");
            DKIM = MailAuthVerdict.Unset;
            logToPopulate.Add("DKIM checking was skipped (501).");
            DMARC = MailAuthVerdict.Unset;
            logToPopulate.Add("DMARC checking was skipped (501).");
        }
    }
}