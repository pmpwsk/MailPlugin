using System.Runtime.Serialization;
using uwap.Database;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    [DataContract]
    public class Mailbox : ITableValue
    {
        [DataMember]
        public readonly string Id;

        [DataMember]
        public readonly string Address;

        [DataMember]
        public string? Name;

        [DataMember]
        public readonly Dictionary<string, HashSet<string>> AllowedUserIds; //first key=usertable.Name, second key/value=user id in the table

        [DataMember]
        public readonly Dictionary<ulong, MailMessage> Messages; //key is the unique id of the message across all folders (timestamp ticks as ulong, then increased by 1 until an empty spot was found

        [DataMember]
        public readonly Dictionary<string, SortedSet<ulong>> Folders; //key is the folder name, value is the list of message ids within it, sorted by time

        [DataMember]
        public readonly Dictionary<string, string> Contacts; //key=address, value=name

        [DataMember]
        public readonly HashSet<string> BlockedAddresses;

        [DataMember]
        public readonly MailAuthRequirements AuthRequirements;

        [DataMember]
        public readonly Dictionary<string, string> CustomSettings;

        public Mailbox(string id, string address)
        {
            Id = id;
            Address = address;
            Name = null;
            AllowedUserIds = [];
            Messages = [];
            Folders = new()
            {
                { "Inbox", [] },
                { "Sent", [] },
                { "Trash", [] },
                { "Spam", [] }
            };
            Contacts = [];
            BlockedAddresses = [];
            AuthRequirements = new();
            CustomSettings = [];
        }
    }
}