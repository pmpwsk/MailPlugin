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
        public MailAuthVerdictSPF SPF;

        [DataMember]
        public MailAuthVerdictDKIM DKIM;

        [DataMember]
        public MailAuthVerdictDMARC DMARC;

        public MailAuthRequirements()
        {
            Secure = true;
            SPF = MailAuthVerdictSPF.Unset;
            DKIM = MailAuthVerdictDKIM.Unset;
            DMARC = MailAuthVerdictDMARC.Unset;
        }

        public bool SatisfiedBy(MailAuthResult result)
            => (result.Secure || !Secure)
            && result.SPF >= SPF
            && result.DKIM >= DKIM
            && result.DMARC >= DMARC;
    }
}