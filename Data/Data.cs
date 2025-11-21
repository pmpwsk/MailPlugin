using System.Diagnostics.CodeAnalysis;
using uwap.WebFramework.Database;
using uwap.WebFramework.Elements;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    private readonly MailboxTable Mailboxes = MailboxTable.Import("MailPlugin.Mailboxes");

    private static readonly IEnumerable<string> DefaultFolders = ["Inbox", "Sent", "Trash", "Spam"];

    /// <summary>
    /// The amount of messages to be listed at a time, and to be checked to determine if a mailbox/folder has unread messages.<br/>
    /// Default: 25
    /// </summary>
    public ushort MessagePreloadCount = 25;

    /// <summary>
    /// Whether to print incoming mail messages to the console if no matching mailbox was found for them.<br/>
    /// Default: false
    /// </summary>
    public bool PrintUnrecognizedToConsole = false;

    /// <summary>
    /// Whether to attempt to send emails to recipients that aren't present in the database, but have a domain that is present in it, externally.<br/>
    /// Default: false
    /// </summary>
    public bool SendMissingInternalRecipientsExternally = false;

    /// <summary>
    /// first key is the mailbox to be listened on, second key is the listening request, value is true if "refresh" should be sent and false if "icon" should be sent
    /// </summary>
    private readonly Dictionary<string, Dictionary<Request, bool>> IncomingListeners = [];

    private Task RemoveIncomingListener(Request req)
    {
        foreach (var kv in IncomingListeners)
            if (kv.Value.Remove(req) && kv.Value.Count == 0)
                IncomingListeners.Remove(kv.Key);
        return Task.CompletedTask;
    }

    private static IEnumerable<ulong> GetLastReversed(SortedSet<ulong> source, int count, int offset)
    {
        int n = source.Count - count - offset;
        if (n < 0)
            count += n;
        return source.Skip(n).Take(count).Reverse();
    }

    private static string DateTimeString(DateTime dt)
        => $"{dt.DayOfWeek}, {dt.Year}/{dt.Month}/{dt.Day}, {dt.ToShortTimeString()}";

    private static readonly string[] FileSizeUnits = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
    private static string FileSizeString(long size)
    {
        if (size == 0)
            return $"0 {FileSizeUnits[0]}";
        int factor = (int)Math.Floor(Math.Log(size, 1000));
        if (factor >= FileSizeUnits.Length)
            factor = FileSizeUnits.Length - 1;
        return $"{Math.Round(size / Math.Pow(1000, factor), 1, MidpointRounding.AwayFromZero)} {FileSizeUnits[factor]}";
    }

    private static DateTime AdjustDateTime(Request req, DateTime dateTime)
    {
        try
        {
            string timeOffset = req.Cookies["TimeOffset"];
            if (timeOffset == "")
                return dateTime;
            else return dateTime.AddMinutes(0 - int.Parse(timeOffset));
        }
        catch
        {
            req.Cookies.Delete("TimeOffset");
            return dateTime;
        }
    }

    private static string Before(string value, string separator)
    {
        int num = value.IndexOf(separator, StringComparison.Ordinal);
        if (num == -1)
        {
            return value;
        }

        return value.Remove(num);
    }

    /// <summary>
    /// Adds an s to the unit if the count isn't 1.
    /// </summary>
    private static string CountString(int count, string unit)
        => $"{count} {unit}{(count == 1 ? "" : "s")}";

    private static IEnumerable<string> SortFolders(IEnumerable<string> folderNames)
    {
        foreach (var f in DefaultFolders)
            yield return f;
        foreach (var f in folderNames.Except(DefaultFolders).Order())
            yield return f;
    }

    private static IEnumerable<KeyValuePair<string,SortedSet<ulong>>> SortFolders(Dictionary<string,SortedSet<ulong>> folders)
    {
        foreach (var key in DefaultFolders)
            yield return new(key, folders[key]);
        foreach (var f in folders.Where(pair => !DefaultFolders.Contains(pair.Key)).OrderBy(x => x.Key))
            yield return f;
    }

    private bool InvalidMailbox(Request req, [MaybeNullWhen(true)] out Mailbox mailbox)
    {
        if (!req.Query.TryGetValue("mailbox", out string? mailboxId))
        {
            req.Status = 400;
            mailbox = null;
            return true;
        }
        if (!Mailboxes.TryGetValue(mailboxId, out mailbox))
        {
            if (req.Page is Page page)
                page.Elements.Add(new LargeContainerElement("Error", "This mailbox doesn't exist!", "red"));
            else req.Status = 404;
            mailbox = null;
            return true;
        }
        if ((!mailbox.AllowedUserIds.TryGetValue(req.UserTable.Name, out var allowedUserIds)) || !allowedUserIds.Contains(req.User.Id))
        {
            if (req.Page is Page page)
                page.Elements.Add(new LargeContainerElement("Error", "You don't have access to this mailbox!", "red"));
            else req.Status = 403;
            mailbox = null;
            return true;
        }
        return false;
    }

    private static bool InvalidMessage(Request req, [MaybeNullWhen(true)] out MailMessage message, out ulong messageId, Mailbox mailbox, bool acceptDraft = false)
    {
        if (!req.Query.TryGetValue("message", out var messageIdString))
        {
            req.Status = 400;
            message = null;
            messageId = 0UL;
            return true;
        }
        if (!ulong.TryParse(messageIdString, out messageId) || (messageId == 0 && !acceptDraft) || !mailbox.Messages.TryGetValue(messageId, out message))
        {
            if (req.Page is Page page)
                page.Elements.Add(new LargeContainerElement("Error", "This message doesn't exist!", "red"));
            else req.Status = 404;
            message = null;
            messageId = 0UL;
            return true;
        }
        return false;
    }

    private static bool InvalidFolder(Request req, [MaybeNullWhen(true)] out SortedSet<ulong> folder, [MaybeNullWhen(true)] out string folderName, Mailbox mailbox, ulong? messageId)
    {
        if (!req.Query.TryGetValue("folder", out folderName))
        {
            req.Status = 400;
            folder = null;
            folderName = null;
            return true;
        }
        if (!mailbox.Folders.TryGetValue(folderName, out folder))
        {
            if (req.Page is Page page)
                page.Elements.Add(new LargeContainerElement("Error", "This folder doesn't exist!", "red"));
            else req.Status = 404;
            folder = null;
            folderName = null;
            return true;
        }
        if (messageId != null && !folder.Contains(messageId.Value))
        {
            if (req.Page is Page page)
                page.Elements.Add(new LargeContainerElement("Error", "This message isn't part of the requested folder!", "red"));
            else req.Status = 404;
            folder = null;
            folderName = null;
            return true;
        }
        return false;
    }

    private bool InvalidMailboxOrMessage(Request req, [MaybeNullWhen(true)] out Mailbox mailbox, [MaybeNullWhen(true)] out MailMessage message, out ulong messageId, bool acceptDraft = false)
    {
        if (InvalidMailbox(req, out mailbox))
        {
            message = null;
            messageId = 0UL;
            return true;
        }
        if (InvalidMessage(req, out message, out messageId, mailbox, acceptDraft))
        {
            mailbox = null;
            return true;
        }
        return false;
    }

    private bool InvalidMailboxOrFolder(Request req, [MaybeNullWhen(true)] out Mailbox mailbox, [MaybeNullWhen(true)] out SortedSet<ulong> folder, [MaybeNullWhen(true)] out string folderName)
    {
        if (InvalidMailbox(req, out mailbox))
        {
            folder = null;
            folderName = null;
            return true;
        }
        if (InvalidFolder(req, out folder, out folderName, mailbox, null))
        {
            mailbox = null;
            return true;
        }
        return false;
    }

    private bool InvalidMailboxOrMessageOrFolder(Request req, [MaybeNullWhen(true)] out Mailbox mailbox, [MaybeNullWhen(true)] out MailMessage message, out ulong messageId, [MaybeNullWhen(true)] out SortedSet<ulong> folder, [MaybeNullWhen(true)] out string folderName, bool acceptDraft = false)
    {
        if (InvalidMailbox(req, out mailbox))
        {
            message = null;
            messageId = 0UL;
            folder = null;
            folderName = null;
            return true;
        }
        if (InvalidMessage(req, out message, out messageId, mailbox, acceptDraft))
        {
            mailbox = null;
            folder = null;
            folderName = null;
            return true;
        }
        if (InvalidFolder(req, out folder, out folderName, mailbox, messageId))
        {
            mailbox = null;
            message = null;
            messageId = 0UL;
            return true;
        }
        return false;
    }
    
    private void DeleteMessage(Mailbox mailbox, List<IFileAction> fileActions, ulong messageId)
    {
        mailbox.Messages.Remove(messageId);
        foreach (var (_, folder) in mailbox.Folders)
            folder.Remove(messageId);
        
        mailbox.DeleteFileIfExists($"{messageId}/html", fileActions);
        mailbox.DeleteFileIfExists($"{messageId}/text", fileActions);
        int attachmentId = 0;
        while (mailbox.DeleteFileIfExists($"{messageId}/{attachmentId}", fileActions))
            attachmentId++;
    }
    
    private IEnumerable<Mailbox> EnumerateAccessibleMailboxes(Request req)
        => EnumerateAccessibleMailboxes(req.UserTable.Name, req.User.Id);
    
    private IEnumerable<Mailbox> EnumerateAccessibleMailboxes(string userTableName, string userId)
        => Mailboxes.EnumerateExistingByIds(Mailboxes.AllowedMailboxesIndex.Get((userTableName, userId)))
            .OrderBy(m => m.Address.After('@')).ThenBy(x => x.Address.Before('@'));
}