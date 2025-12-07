using System.Diagnostics.CodeAnalysis;
using uwap.WebFramework.Database;
using uwap.WebFramework.Elements;
using uwap.WebFramework.Responses;

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
    private readonly Dictionary<string, Dictionary<EventResponse, bool>> IncomingListeners = [];

    private Task RemoveIncomingListener(Request req, EventResponse response)
    {
        foreach (var kv in IncomingListeners)
            if (kv.Value.Remove(response) && kv.Value.Count == 0)
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
            req.CookieWriter?.Delete("TimeOffset");
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
    
    private async Task<Mailbox?> TryGetMailboxAsync(Request req)
    {
        if (req.Query.ContainsKey("mailbox"))
            return await ValidateMailboxAsync(req);
        return null;
    }

    private async Task<Mailbox> ValidateMailboxAsync(Request req)
    {
        var mailboxId = req.Query.GetOrThrow("mailbox");
        var mailbox = await Mailboxes.GetByIdAsync(mailboxId);
        if ((!mailbox.AllowedUserIds.TryGetValue(req.UserTable.Name, out var allowedUserIds)) || !allowedUserIds.Contains(req.User.Id))
            throw new ForcedResponse(StatusResponse.Forbidden);
        return mailbox;
    }

    private static (ulong MessageId, MailMessage Message) ValidateMessage(Request req, Mailbox mailbox, bool acceptDraft = false)
    {
        var messageIdString = req.Query.GetOrThrow("message");
        if (!ulong.TryParse(messageIdString, out var messageId) || (messageId == 0 && !acceptDraft) || !mailbox.Messages.TryGetValue(messageId, out var message))
            throw new ForcedResponse(StatusResponse.NotFound);
        return (messageId, message);
    }

    private static (string FolderName, SortedSet<ulong> Folder) ValidateFolder(Request req, Mailbox mailbox, ulong? messageId)
    {
        var folderName = req.Query.GetOrThrow("folder");
        if (!mailbox.Folders.TryGetValue(folderName, out var folder) || messageId != null && !folder.Contains(messageId.Value))
            throw new ForcedResponse(StatusResponse.NotFound);
        return (folderName, folder);
    }

    private async Task<(Mailbox Mailbox, ulong MessageId, MailMessage Message)> ValidateMailboxAndMessageAsync(Request req, bool acceptDraft = false)
    {
        var mailbox = await ValidateMailboxAsync(req);
        var (messageId, message) = ValidateMessage(req, mailbox, acceptDraft);
        return (mailbox, messageId, message);
    }

    private async Task<(Mailbox Mailbox, string FolderName, SortedSet<ulong> Folder)> ValidateMailboxAndFolderAsync(Request req)
    {
        var mailbox = await ValidateMailboxAsync(req);
        var (folderName, folder) = ValidateFolder(req, mailbox, null);
        return (mailbox, folderName, folder);
    }

    private async Task<(Mailbox Mailbox, ulong MessageId, MailMessage Message, string FolderName, SortedSet<ulong> Folder)> ValidateMailboxAndMessageAndFolderAsync(Request req, bool acceptDraft = false)
    {
        var mailbox = await ValidateMailboxAsync(req);
        var (messageId, message) = ValidateMessage(req, mailbox, acceptDraft);
        var (folderName, folder) = ValidateFolder(req, mailbox, messageId);
        return (mailbox, messageId, message, folderName, folder);
    }
    
    private static void DeleteMessage(Mailbox mailbox, List<IFileAction> fileActions, ulong messageId)
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
    
    private Task<List<Mailbox>> ListAccessibleMailboxesAsync(Request req)
        => ListAccessibleMailboxesAsync(req.UserTable.Name, req.User.Id);
    
    private async Task<List<Mailbox>> ListAccessibleMailboxesAsync(string userTableName, string userId)
        => (await Mailboxes.ListExistingByIdsAsync(await Mailboxes.AllowedMailboxesIndex.GetAsync((userTableName, userId))))
            .OrderBy(m => m.Address.After('@')).ThenBy(x => x.Address.Before('@')).ToList();
}