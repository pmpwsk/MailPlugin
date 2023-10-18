﻿using MimeKit;
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
        public readonly MailAuthVerdict DKIM;

        [DataMember]
        public readonly MailAuthVerdict DMARC;

        public MailAuthResult(MailConnectionData connectionData, MimeMessage message, List<string> logToPopulate)
        {
            IPAddress = connectionData.IP.Address.ToString();
            Secure = connectionData.Secure;

            logToPopulate.Add("From: " + IPAddress);
            logToPopulate.Add("Secure: " + Secure.ToString());

            SPF = MailAuthVerdictSPF.Unset;
            logToPopulate.Add("SPF checking was skipped (501).");
            DKIM = MailAuthVerdict.Unset;
            logToPopulate.Add("DKIM checking was skipped (501).");
            DMARC = MailAuthVerdict.Unset;
            logToPopulate.Add("DMARC checking was skipped (501).");
        }
    }
}