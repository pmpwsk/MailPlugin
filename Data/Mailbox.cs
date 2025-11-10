using System.Runtime.Serialization;
using uwap.Database;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    [DataContract]
    public class Mailbox(string address) : AbstractTableValue
    {
        [DataMember]
        public readonly string Address = address;

        [DataMember]
        public string? Name = null;

        [DataMember]
        public readonly Dictionary<string, HashSet<string>> AllowedUserIds = []; //first key=usertable.Name, second key/value=user id in the table

        [DataMember]
        public readonly Dictionary<ulong, MailMessage> Messages = []; //key is the unique id of the message across all folders (timestamp ticks as ulong, then increased by 1 until an empty spot was found

        [DataMember]
        public readonly Dictionary<string, SortedSet<ulong>> Folders = new()
        {
            { "Inbox", [] },
            { "Sent", [] },
            { "Trash", [] },
            { "Spam", [] }
        }; //key is the folder name, value is the list of message ids within it, sorted by time

        [DataMember]
        public readonly Dictionary<string, MailContact> Contacts = []; //key=address, value=contact object

        [DataMember]
        public readonly HashSet<string> BlockedAddresses = [];

        [DataMember]
        public string? Footer = null;

        [DataMember]
        public readonly MailAuthRequirements AuthRequirements = new();

        [DataMember]
        public readonly Dictionary<string, string> CustomSettings = [];

        [DataMember]
        public bool ShowExternalImageLinks = false;
        
        protected override void Migrate(string tableName, string id, byte[] serialized)
        {
            if (AssemblyVersion == new Version(0, 0, 0, 0))
            {
                var dir = new DirectoryInfo($"../MailPlugin.Mailboxes/{id}");
                if (dir.Exists)
                {
                    foreach (var messageDir in dir.GetDirectories("*", SearchOption.TopDirectoryOnly))
                        foreach (var file in messageDir.GetFiles("*", SearchOption.TopDirectoryOnly))
                            MigrateLegacyFile(tableName, id, $"{messageDir.Name}/{file.Name}", file.FullName);
                    dir.Delete(true);
                }
            }
        }
    }
}