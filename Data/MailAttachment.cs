using System.Runtime.Serialization;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    [DataContract]
    public class MailAttachment
    {
        [DataMember]
        public readonly string? Name;

        [DataMember]
        public readonly string? MimeType;

        public MailAttachment(string? name, string? mimeType)
        {
            Name = name;
            MimeType = mimeType;
        }
    }
}