using uwap.WebFramework.Mail;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    /// <param name="domain">The domain to look up, including the specific subdomain such as {selector}._domainkey for DKIM or _dmarc for DMARC.</param>
    /// <param name="protocol">The protocol (case insensitive), such as spf1. DKIM and DMARC use null because their protocol field is optional. </param>
    /// <param name="requiredFields">Every subarray of this array must have at least one item present as a field key.</param>
    private static List<KeyValuePair<string,string?>>? ResolveTXT(string domain, string? protocol, string[][] requiredFields)
    {
        var query = MailManager.DnsLookup.Query(domain, DnsClient.QueryType.TXT);
        var records = query.Answers.TxtRecords();
        foreach (string value in records.SelectMany(x => x.Text))
        {
            var fields = value.Split(' ', ';').Where(x => x != "");
            if (!fields.Any())
                continue;
            if (protocol != null && fields.First().ToLower() != $"v={protocol.ToLower()}")
                continue;
            List<KeyValuePair<string, string?>> result = new();
            foreach (string field in fields)
            {
                int split = field.IndexOfAny(new[] { '=', ':' });
                if (split == -1)
                    result.Add(new(field, null));
                else
                {
                    string k = field.Remove(split);
                    if (k != "v")
                        result.Add(new(k, field.Remove(0, split + 1)));
                }
            }
            if (requiredFields.All(x => x.Any(y => result.Any(z => z.Key == y))))
                return result;
        }
        return null;
    }
}