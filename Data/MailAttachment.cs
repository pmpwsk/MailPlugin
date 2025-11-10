using System.Runtime.Serialization;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    [DataContract]
    public class MailAttachment(string? name, string? mimeType)
    {
        [DataMember]
        public readonly string? Name = name;

        [DataMember]
        public readonly string? MimeType = mimeType;
    }
}