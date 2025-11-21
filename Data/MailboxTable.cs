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
        
        public new static MailboxTable Import(string name)
            => Tables.Dictionary.TryGetValue(name, out AbstractTable? existingTable) ? (MailboxTable)existingTable : new MailboxTable(name);
    }
}