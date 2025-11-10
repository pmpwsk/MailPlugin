using System.Runtime.Serialization;
using static uwap.WebFramework.Mail.MailAuth;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    [DataContract]
    public class MailAuthRequirements
    {
        [DataMember] public bool Secure = true;
        [DataMember] public bool PTR = true;
        [DataMember] public bool SatisfiedByDMARC = true;
        [DataMember] public MailAuthVerdictSPF SPF = MailAuthVerdictSPF.Unset;
        [DataMember] public MailAuthVerdictDKIM DKIM = MailAuthVerdictDKIM.Unset;
        [DataMember] public MailAuthVerdictDMARC DMARC = MailAuthVerdictDMARC.Unset;

        public bool SatisfiedBy(FullResult result)
            => (result.Secure || !Secure)
            && (result.PTR != null || !PTR)
            && ((SatisfiedByDMARC && result.DMARC == MailAuthVerdictDMARC.Pass)
                || (result.SPF >= SPF && result.DKIM >= DKIM && result.DMARC >= DMARC));
    }
}