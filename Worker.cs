namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public override Task Work()
    {
        Mailboxes.RebuildAccelerators();

        foreach (var mailboxKV in Mailboxes)
        {
            Mailbox mailbox = mailboxKV.Value;
            List<ulong> due = [];
            foreach (var messageKV in mailboxKV.Value.Messages)
            {
                DateTime? deleted = messageKV.Value.Deleted;
                if (deleted != null && (deleted.Value + TimeSpan.FromDays(30)) <= DateTime.UtcNow)
                    due.Add(messageKV.Key);
            }
            if (due.Count > 0)
            {
                mailbox.Lock();
                foreach (ulong id in due)
                    mailbox.Messages.Remove(id);
                mailbox.UnlockSave();
            }    
        }

        return Task.CompletedTask;
    }
}