﻿using System.Runtime.Serialization;
using static uwap.WebFramework.Mail.MailAuth;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    [DataContract]
    public class MailAuthRequirements
    {
        [DataMember]
        public bool Secure;

        [DataMember]
        public bool PTR;

        [DataMember]
        public bool SatisfiedByDMARC;

        [DataMember]
        public MailAuthVerdictSPF SPF;

        [DataMember]
        public MailAuthVerdictDKIM DKIM;

        [DataMember]
        public MailAuthVerdictDMARC DMARC;

        public MailAuthRequirements()
        {
            Secure = true;
            PTR = true;
            SatisfiedByDMARC = true;
            SPF = MailAuthVerdictSPF.Unset;
            DKIM = MailAuthVerdictDKIM.Unset;
            DMARC = MailAuthVerdictDMARC.Unset;
        }

        public bool SatisfiedBy(FullResult result)
            => (result.Secure || !Secure)
            && result.SPF >= SPF
            && result.DKIM >= DKIM
            && result.DMARC >= DMARC;
    }
}