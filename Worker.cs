namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public override async Task Work()
    {
        foreach (var listedMailbox in await Mailboxes.ListAllAsync())
        {
            List<ulong> due = [];
            foreach (var messageKV in listedMailbox.Messages)
            {
                DateTime? deleted = messageKV.Value.Deleted;
                if (deleted != null && (deleted.Value + TimeSpan.FromDays(30)) <= DateTime.UtcNow)
                    due.Add(messageKV.Key);
            }
            
            if (due.Count > 0)
                await Mailboxes.TransactionIgnoreNullAsync(listedMailbox.Id, t =>
                {
                    foreach (ulong messageId in due)
                        DeleteMessage(t.Value, t.FileActions, messageId);
                });
        }
    }
}