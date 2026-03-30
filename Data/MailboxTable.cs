using uwap.WebFramework.Database;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public class MailboxTable(string name, List<ClusterNode> clusterNodes) : Table<Mailbox>(name, clusterNodes)
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
        
        public new static MailboxTable Import(string name, List<ClusterNode> clusterNodes)
            => Tables.TryGetTable<MailboxTable>(name) ?? new MailboxTable(name, clusterNodes);

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
                    if (minimal.State.Deleted)
                        serialized = current.Serialized;
                    else
                    {
                        var mailbox = Serializers.DataContractJson.Deserialize<Mailbox>(current.Serialized);
                        
                        var dir = new DirectoryInfo($"../MailPlugin.Mailboxes/{id}");
                        if (dir.Exists)
                        {
                            foreach (var messageDir in dir.GetDirectories("*", SearchOption.TopDirectoryOnly))
                                foreach (var file in messageDir.GetFiles("*", SearchOption.TopDirectoryOnly))
                                    mailbox.MigrateLegacyFile(this, id, $"{messageDir.Name}/{file.Name}", file.FullName);
                            dir.Delete(true);
                        }
                        
                        serialized = Serializers.DataContractJson.Serialize(mailbox);
                    }
                    return (serialized, 1);
                }
                default:
                    return null;
            }
        }
    }
}