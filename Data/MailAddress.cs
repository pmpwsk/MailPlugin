using MimeKit;
using System.Runtime.Serialization;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    [DataContract]
    public class MailAddress(string address, string name)
    {
        [DataMember]
        public readonly string Address = address;

        [DataMember]
        public readonly string Name = name;

        public MailAddress(MailboxAddress ma)
            : this(ma.Address, ma.Name) { }

        public string LocalPart => Address.Before('@');
        public string HostPart => Address.After('@');
        public string FullString
            => (Name == null || Name == "" || Name == Address || Name == LocalPart) ? Address : $"{Name} ({Address})";
    }
}