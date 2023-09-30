using System.Runtime.Serialization;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    [DataContract]
    public class MailAuthRequirements
    {
        [DataMember]
        public bool Secure;

        [DataMember]
        public MailAuthVerdict SPF;

        [DataMember]
        public MailAuthVerdict DKIM;

        [DataMember]
        public MailAuthVerdict DMARC;

        public MailAuthRequirements()
        {
            Secure = true;
            SPF = MailAuthVerdict.Unset;
            DKIM = MailAuthVerdict.Unset;
            DMARC = MailAuthVerdict.Unset;
        }

        public bool SatisfiedBy(MailAuthResult result)
            => (result.Secure || !Secure)
            && result.SPF >= SPF
            && result.DKIM >= DKIM
            && result.DMARC >= DMARC;
    }
}