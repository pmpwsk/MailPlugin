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

    private bool ValidMailbox(IRequest req, [MaybeNullWhen(false)] out Mailbox mailbox)
    {
        if (req.User == null)
        {
            req.Status = 403;
            mailbox = null;
            return false;
        }
        if (!req.Query.TryGetValue("mailbox", out string? mailboxId))
        {
            req.Status = 400;
            mailbox = null;
            return false;
        }
        if (!Mailboxes.TryGetValue(mailboxId, out mailbox))
        {
            req.Status = 404;
            mailbox = null;
            return false;
        }
        if ((!mailbox.AllowedUserIds.TryGetValue(req.UserTable.Name, out var allowedUserIds)) || !allowedUserIds.Contains(req.User.Id))
        {
            req.Status = 403;
            mailbox = null;
            return false;
        }
        return true;
    }
}