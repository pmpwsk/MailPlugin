using System.Globalization;
using System.Text;

namespace MimeKit.Cryptography;

public static class MimeKitCryptographyUser
{
    public static Dictionary<string, string> ParseParameterTags(HeaderId header, string signature)
    {
        var parameters = new Dictionary<string, string>();
        var value = new StringBuilder();
        int index = 0;

        while (index < signature.Length)
        {
            while (index < signature.Length && IsWhiteSpace(signature[index]))
                index++;

            if (index >= signature.Length)
                break;

            if (signature[index] == ';' || !IsAlpha(signature[index]))
                throw new FormatException(string.Format("Malformed {0} value.", header.ToHeaderName()));

            int startIndex = index++;

            while (index < signature.Length && signature[index] != '=')
                index++;

            if (index >= signature.Length)
                continue;

            var name = signature.AsSpan(startIndex, index - startIndex).TrimEnd().ToString();

            // skip over '=' and clear value buffer
            value.Length = 0;
            index++;

            while (index < signature.Length && signature[index] != ';')
            {
                if (!IsWhiteSpace(signature[index]))
                    value.Append(signature[index]);
                index++;
            }

            if (parameters.ContainsKey(name))
                throw new FormatException(string.Format("Malformed {0} value: duplicate parameter '{1}'.", header.ToHeaderName(), name));

            parameters.Add(name, value.ToString());

            // skip over ';'
            index++;
        }

        return parameters;
    }

    static bool IsWhiteSpace(char c)
    {
        return c == ' ' || c == '\t';
    }

    static bool IsAlpha(char c)
    {
        return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
    }

    public static void ValidateDkimSignatureParameters(IDictionary<string, string> parameters, out DkimSignatureAlgorithm algorithm, out DkimCanonicalizationAlgorithm headerAlgorithm,
            out DkimCanonicalizationAlgorithm bodyAlgorithm, out string? d, out string? s, out string? q, out string[] headers, out string? bh, out string? b, out int maxLength)
    {
        bool containsFrom = false;

        if (!parameters.TryGetValue("v", out string? v))
            throw new FormatException("Malformed DKIM-Signature header: no version parameter detected.");

        if (v != "1")
            throw new FormatException(string.Format("Unrecognized DKIM-Signature version: v={0}", v));

        ValidateCommonSignatureParameters("DKIM-Signature", parameters, out algorithm, out headerAlgorithm, out bodyAlgorithm, out d, out s, out q, out headers, out bh, out b, out maxLength);

        for (int i = 0; i < headers.Length; i++)
        {
            if (headers[i].Equals("from", StringComparison.OrdinalIgnoreCase))
            {
                containsFrom = true;
                break;
            }
        }

        if (!containsFrom)
            throw new FormatException("Malformed DKIM-Signature header: From header not signed.");

        if (parameters.TryGetValue("i", out string? id))
        {
            int at;

            if ((at = id.LastIndexOf('@')) == -1)
                throw new FormatException("Malformed DKIM-Signature header: no @ in the AUID value.");

            var ident = id.AsSpan(at + 1);

            if (!ident.Equals(d.AsSpan(), StringComparison.OrdinalIgnoreCase) && !ident.EndsWith(("." + d).AsSpan(), StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Invalid DKIM-Signature header: the domain in the AUID does not match the domain parameter.");
        }
    }

    static void ValidateCommonSignatureParameters(string header, IDictionary<string, string> parameters, out DkimSignatureAlgorithm algorithm, out DkimCanonicalizationAlgorithm headerAlgorithm,
            out DkimCanonicalizationAlgorithm bodyAlgorithm, out string? d, out string? s, out string? q, out string[] headers, out string? bh, out string? b, out int maxLength)
    {
        ValidateCommonParameters(header, parameters, out algorithm, out d, out s, out q, out b);

        if (parameters.TryGetValue("l", out string? l))
        {
            if (!int.TryParse(l, NumberStyles.None, CultureInfo.InvariantCulture, out maxLength) || maxLength < 0)
                throw new FormatException(string.Format("Malformed {0} header: invalid length parameter: l={1}", header, l));
        }
        else
        {
            maxLength = -1;
        }

        if (parameters.TryGetValue("c", out string? c))
        {
            var tokens = c.ToLowerInvariant().Split('/');

            if (tokens.Length == 0 || tokens.Length > 2)
                throw new FormatException(string.Format("Malformed {0} header: invalid canonicalization parameter: c={1}", header, c));

            switch (tokens[0])
            {
                case "relaxed": headerAlgorithm = DkimCanonicalizationAlgorithm.Relaxed; break;
                case "simple": headerAlgorithm = DkimCanonicalizationAlgorithm.Simple; break;
                default: throw new FormatException(string.Format("Malformed {0} header: invalid canonicalization parameter: c={1}", header, c));
            }

            if (tokens.Length == 2)
            {
                switch (tokens[1])
                {
                    case "relaxed": bodyAlgorithm = DkimCanonicalizationAlgorithm.Relaxed; break;
                    case "simple": bodyAlgorithm = DkimCanonicalizationAlgorithm.Simple; break;
                    default: throw new FormatException(string.Format("Malformed {0} header: invalid canonicalization parameter: c={1}", header, c));
                }
            }
            else
            {
                bodyAlgorithm = DkimCanonicalizationAlgorithm.Simple;
            }
        }
        else
        {
            headerAlgorithm = DkimCanonicalizationAlgorithm.Simple;
            bodyAlgorithm = DkimCanonicalizationAlgorithm.Simple;
        }

        if (!parameters.TryGetValue("h", out string? h))
            throw new FormatException(string.Format("Malformed {0} header: no signed header parameter detected.", header));

        headers = h.Split(':');

        if (!parameters.TryGetValue("bh", out bh))
            throw new FormatException(string.Format("Malformed {0} header: no body hash parameter detected.", header));
    }

    static void ValidateCommonParameters(string header, IDictionary<string, string> parameters, out DkimSignatureAlgorithm algorithm,
            out string? d, out string? s, out string? q, out string? b)
    {
        if (!parameters.TryGetValue("a", out string? a))
            throw new FormatException(string.Format("Malformed {0} header: no signature algorithm parameter detected.", header));

        switch (a.ToLowerInvariant())
        {
            case "ed25519-sha256": algorithm = DkimSignatureAlgorithm.Ed25519Sha256; break;
            case "rsa-sha256": algorithm = DkimSignatureAlgorithm.RsaSha256; break;
            case "rsa-sha1": algorithm = DkimSignatureAlgorithm.RsaSha1; break;
            default: throw new FormatException(string.Format("Unrecognized {0} algorithm parameter: a={1}", header, a));
        }

        if (!parameters.TryGetValue("d", out d))
            throw new FormatException(string.Format("Malformed {0} header: no domain parameter detected.", header));

        if (d.Length == 0)
            throw new FormatException(string.Format("Malformed {0} header: empty domain parameter detected.", header));

        if (!parameters.TryGetValue("s", out s))
            throw new FormatException(string.Format("Malformed {0} header: no selector parameter detected.", header));

        if (s.Length == 0)
            throw new FormatException(string.Format("Malformed {0} header: empty selector parameter detected.", header));

        if (!parameters.TryGetValue("q", out q))
            q = "dns/txt";

        if (!parameters.TryGetValue("b", out b))
            throw new FormatException(string.Format("Malformed {0} header: no signature parameter detected.", header));

        if (b.Length == 0)
            throw new FormatException(string.Format("Malformed {0} header: empty signature parameter detected.", header));

        if (parameters.TryGetValue("t", out string? t))
        {
            if (!int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out int timestamp) || timestamp < 0)
                throw new FormatException(string.Format("Malformed {0} header: invalid timestamp parameter: t={1}.", header, t));
        }
    }
}