using uwap.WebFramework.Database;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public override Task Work()
    {
        foreach (var listedMailbox in Mailboxes.ListAll())
        {
            List<ulong> due = [];
            foreach (var messageKV in listedMailbox.Messages)
            {
                DateTime? deleted = messageKV.Value.Deleted;
                if (deleted != null && (deleted.Value + TimeSpan.FromDays(30)) <= DateTime.UtcNow)
                    due.Add(messageKV.Key);
            }
            
            if (due.Count > 0)
                Mailboxes.TransactionIgnoreNull(listedMailbox.Id, (ref Mailbox mailbox, ref List<IFileAction> fileActions) =>
                {
                    foreach (ulong messageId in due)
                        DeleteMessage(mailbox, fileActions, messageId);
                });
        }

        return Task.CompletedTask;
    }
}