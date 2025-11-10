using System.Runtime.Serialization;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    [DataContract]
    public class MailContact(string name, bool favorite = false)
    {
        [DataMember] public string Name = name;
        [DataMember] public bool Favorite = favorite;
    }
}