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
}