using uwap.WebFramework.Database;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public class MailboxTable(string name) : Table<Mailbox>(name)
    {
        public UniqueTableIndex<Mailbox, string> AddressIndex = new(mailbox => mailbox.Address);
        
        public M2MTableIndex<Mailbox, (string tableName, string userId)> AllowedMailboxesIndex = new(mailbox =>
        {
            var result = new List<(string, string)>();
            foreach (var (tableName, userIds) in mailbox.AllowedUserIds)
                foreach (var userId in userIds)
                    result.Add((tableName, userId));
            return result;
        });

        protected override IEnumerable<ITableIndex<Mailbox>> Indices => [ AddressIndex, AllowedMailboxesIndex ];
        
        public static MailboxTable Import(string name)
            => Tables.Dictionary.TryGetValue(name, out AbstractTable? existingTable) ? (MailboxTable)existingTable : new MailboxTable(name);

        public override ulong TypeIteration
            => 1;
    
        public override AbstractSerializer Serializer
            => Serializers.DataContractJson;

        protected override (byte[] Serialized, ulong TypeIteration)? UpgradeStep(string id, (byte[] Serialized, ulong TypeIteration) current)
        {
            switch (current.TypeIteration)
            {
                case 0:
                {
                    byte[] serialized;
                    var minimal = Serializers.DataContractJson.Deserialize<MinimalTableValue>(current.Serialized);
                    if (minimal.Deleted)
                        serialized = current.Serialized;
                    else
                    {
                        var mailbox = Serializers.DataContractJson.Deserialize<Mailbox>(current.Serialized);
                        var dirty = mailbox.EnsureMinimalTableValue();
                        
                        var dir = new DirectoryInfo($"../MailPlugin.Mailboxes/{id}");
                        if (dir.Exists)
                        {
                            dirty = true;
                            foreach (var messageDir in dir.GetDirectories("*", SearchOption.TopDirectoryOnly))
                                foreach (var file in messageDir.GetFiles("*", SearchOption.TopDirectoryOnly))
                                    mailbox.MigrateLegacyFile(this, id, $"{messageDir.Name}/{file.Name}", file.FullName);
                            dir.Delete(true);
                        }
                        
                        serialized = dirty ? Serializers.DataContractJson.Serialize(mailbox) : current.Serialized;
                    }
                    return (serialized, 1);
                }
                default:
                    return null;
            }
        }
    }
}