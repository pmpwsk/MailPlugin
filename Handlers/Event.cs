namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    /// <summary>
    /// first key is the mailbox to be listened on, second key is the listening request, value is true if "refresh" should be sent and false if "icon" should be sent
    /// </summary>
    private readonly Dictionary<Mailbox, Dictionary<EventRequest, bool>> IncomingListeners = [];

    private Task RemoveIncomingListener(object requestObject)
    {
        if (requestObject is EventRequest req)
        {
            foreach (var kv in IncomingListeners)
                if (kv.Value.Remove(req) && kv.Value.Count == 0)
                    IncomingListeners.Remove(kv.Key);
        }
        return Task.CompletedTask;
    }

    public override async Task Handle(EventRequest req, string path, string pathPrefix)
    {
        if (!req.LoggedIn)
        {
            req.Status = 403;
            return;
        }

        switch (path)
        {
            case "/incoming":
                req.Context.Response.OnCompleted(RemoveIncomingListener, req);
                //mailboxes - refresh for all
                //mailbox - refresh for mailbox, icon for all others
                //inbox - refresh for mailbox, icon for all others
                //other folder - icon for all
                var mailboxes = Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : [];
                ulong actualLast;
                if (InvalidMailbox(req, out var mailbox))
                {
                    req.Status = 200;
                    actualLast = mailboxes.Max(LastInboxMessageId);
                    //refresh on all mailboxes
                    foreach (var m in mailboxes)
                    {
                        if (IncomingListeners.TryGetValue(m, out var kv))
                            kv[req] = true;
                        else IncomingListeners[m] = new() { { req, true } };
                    }
                }
                else if ((!req.Query.TryGetValue("folder", out var folderName)) || folderName == "Inbox")
                {
                    actualLast = LastInboxMessageId(mailbox);
                    //refresh for mailbox, icon for all others
                    foreach (var m in mailboxes)
                    {
                        if (IncomingListeners.TryGetValue(m, out var kv))
                            kv[req] = m == mailbox;
                        else IncomingListeners[m] = new() { { req, m == mailbox } };
                    }
                }
                else
                {
                    actualLast = LastInboxMessageId(mailbox);
                    //icon for all mailboxes
                    foreach (var m in mailboxes)
                    {
                        if (IncomingListeners.TryGetValue(m, out var kv))
                            kv[req] = false;
                        else IncomingListeners[m] = new() { { req, false } };
                    }
                }
                //check if the event should already be called
                if (req.Query.TryGetValue("last", out var lastString) && ulong.TryParse(lastString, out var last) && actualLast > last)
                {
                    await req.Send("icon");
                    await Task.Delay(5000); //wait a few seconds so it doesn't violently refresh in case something is broken
                    await req.Send("refresh");
                }
                //keep alive
                await req.KeepAlive(req.Context.RequestAborted);
                break;
        }
    }
}