using System.Diagnostics.CodeAnalysis;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private readonly MailboxTable Mailboxes = MailboxTable.Import("Mail");

    private const ushort MessagePreloadCount = 25;

    private static IEnumerable<ulong> GetLastReversed(IEnumerable<ulong> source, int count, int offset)
    {
        int n = source.Count() - count - offset;
        if (n < 0)
            count += n;
        return source.Skip(n).Take(count).Reverse();
    }

    private static string DateTimeString(DateTime dt)
        => $"{dt.DayOfWeek}, {dt.Year}/{dt.Month}/{dt.Day}, {dt.ToShortTimeString()}";

    private bool InvalidMailbox(IRequest req, [MaybeNullWhen(true)] out Mailbox mailbox)
    {
        if (req.User == null)
        {
            req.Status = 403;
            mailbox = null;
            return true;
        }
        if (!req.Query.TryGetValue("mailbox", out string? mailboxId))
        {
            req.Status = 400;
            mailbox = null;
            return true;
        }
        if (!Mailboxes.TryGetValue(mailboxId, out mailbox))
        {
            req.Status = 404;
            mailbox = null;
            return true;
        }
        if ((!mailbox.AllowedUserIds.TryGetValue(req.UserTable.Name, out var allowedUserIds)) || !allowedUserIds.Contains(req.User.Id))
        {
            req.Status = 403;
            mailbox = null;
            return true;
        }
        return false;
    }

    private bool InvalidMessage(IRequest req, [MaybeNullWhen(true)] out MailMessage message, [MaybeNullWhen(true)] out ulong messageId, Mailbox mailbox)
    {
        if (!req.Query.TryGetValue("message", out var messageIdString))
        {
            req.Status = 400;
            message = null;
            messageId = default;
            return true;
        }
        if ((!ulong.TryParse(messageIdString, out messageId)) || messageId == 0 || !mailbox.Messages.TryGetValue(messageId, out message))
        {
            req.Status = 404;
            message = null;
            messageId = default;
            return true;
        }
        return false;
    }

    private bool InvalidFolder(IRequest req, [MaybeNullWhen(true)] out SortedSet<ulong> folder, [MaybeNullWhen(true)] out string folderName, Mailbox mailbox, ulong? messageId)
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
            req.Status = 404;
            folder = null;
            folderName = null;
            return true;
        }
        if (messageId != null && !folder.Contains(messageId.Value))
        {
            req.Status = 404;
            folder = null;
            folderName = null;
            return true;
        }
        return false;
    }
}