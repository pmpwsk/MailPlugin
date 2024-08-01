using uwap.Database;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public class MailboxTable : Table<Mailbox>
    {
        internal Dictionary<string,Dictionary<string,HashSet<Mailbox>>> UserAllowedMailboxes = []; //first key=usertable.Name, second key=user.Id, value=list of allowed mailboxes
        internal Dictionary<string,Mailbox> MailboxByAddress = []; //key is the mail address, value is the mailbox

        private MailboxTable(string name) : base(name) { }

        protected static new MailboxTable Create(string name)
        {
            if (!name.All(Tables.KeyChars.Contains)) throw new Exception($"This name contains characters that are not part of Tables.KeyChars ({Tables.KeyChars}).");
            if (Directory.Exists("../Database/" + name)) throw new Exception("A table with this name already exists, try importing it instead.");
            Directory.CreateDirectory("../Database/" + name);
            MailboxTable table = new(name);
            Tables.Dictionary[name] = table;
            return table;
        }

        public static new MailboxTable Import(string name, bool skipBroken = false)
        {
            if (Tables.Dictionary.TryGetValue(name, out ITable? table)) return (MailboxTable)table;
            if (!name.All(Tables.KeyChars.Contains)) throw new Exception($"This name contains characters that are not part of Tables.KeyChars ({Tables.KeyChars}).");
            if (!Directory.Exists("../Database/" + name)) return Create(name);

            if (Directory.Exists("../Database/Buffer/" + name) && Directory.GetFiles("../Database/Buffer/" + name, "*.json", SearchOption.AllDirectories).Length > 0)
                Console.WriteLine($"The database buffer of table '{name}' contains an entry because a database operation was interrupted. Please manually merge the files and delete the file from the buffer.");

            MailboxTable result = new(name);
            result.Reload(skipBroken);
            Tables.Dictionary[name] = result;
            return result;
        }

        internal static void AddToAccelerators(Mailbox mailbox, Dictionary<string,Dictionary<string,HashSet<Mailbox>>> access, Dictionary<string, Mailbox> mailboxByAddress)
        {
            foreach (var kv in mailbox.AllowedUserIds)
            {
                string userTable = kv.Key;
                if (!access.TryGetValue(userTable, out var userTableDict))
                {
                    userTableDict = [];
                    access[userTable] = userTableDict;
                }
                foreach (string userId in kv.Value)
                {
                    if (!userTableDict.TryGetValue(userId, out var userDict))
                    {
                        userDict = [];
                        userTableDict[userId] = userDict;
                    }
                    userDict.Add(mailbox);
                }
            }
            mailboxByAddress[mailbox.Address] = mailbox;
        }

        internal static void RemoveFromAccelerators(Mailbox mailbox, Dictionary<string,Dictionary<string,HashSet<Mailbox>>> access, Dictionary<string,Mailbox> mailboxByAddress)
        {
            foreach (var kv in mailbox.AllowedUserIds)
            {
                string userTable = kv.Key;
                if (access.TryGetValue(userTable, out var userTableDict))
                {
                    foreach (string userId in kv.Value)
                    {
                        if (userTableDict.TryGetValue(userId, out var userDict))
                        {
                            userDict.Remove(mailbox);

                            if (userDict.Count == 0)
                                userTableDict.Remove(userId);
                        }
                    }

                    if (userTableDict.Count == 0)
                        access.Remove(userTable);
                }
            }
            mailboxByAddress.Remove(mailbox.Address);
        }

        public override void Reload(bool skipBroken = false)
        {
            base.Reload(skipBroken);
            RebuildAccelerators();
        }

        public override Mailbox this[string key]
        {
            get => base[key];
            set
            {
                if (Data.TryGetValue(key, out var oldUser))
                {
                    RemoveFromAccelerators(oldUser.Value, UserAllowedMailboxes, MailboxByAddress);
                }
                base[key] = value;
                AddToAccelerators(value, UserAllowedMailboxes, MailboxByAddress);
            }
        }

        public override bool Delete(string key)
        {
            if (!Data.TryGetValue(key, out var entry)) return false;
            RemoveFromAccelerators(entry.Value, UserAllowedMailboxes, MailboxByAddress);
            base.Delete(key);
            return true;
        }

        public void RebuildAccelerators()
        {
            Dictionary<string, Dictionary<string, HashSet<Mailbox>>> access = [];
            foreach (var entry in Data.Values)
            {
                AddToAccelerators(entry.Value, access, MailboxByAddress);
            }
            UserAllowedMailboxes = access;
        }

        protected override IEnumerable<string> EnumerateDirectoriesToClear()
        {
            yield return "../MailPlugin.Mailboxes";
        }

        protected override IEnumerable<string> EnumerateOtherDirectories(TableEntry<Mailbox> entry)
        {
            yield return $"../MailPlugin.Mailboxes/{entry.Key}";
        }
    }
}