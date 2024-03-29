﻿using System.Text;
namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    internal static string AddHTML(string text)
    {
        string[] lines = text.Replace("&#13;", "").Replace("&#10;", "\n").Replace("\r", "").Trim().Split('\n');
        if (lines.Length == 0 || (lines.Length == 1 && lines[0] == ""))
            return "";

        StringBuilder result = new();
        bool listOpen = false;
        bool addNewLine = false;
        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("- "))
            {
                if (!listOpen)
                {
                    result.Append("<ul>");
                    listOpen = true;
                }
                result.Append("<li>" + trimmed[2..].TrimStart() + "</li>");
            }
            else
            {
                if (listOpen)
                {
                    result.Append("</ul>");
                    addNewLine = false;
                    listOpen = false;
                }
                if (addNewLine)
                    result.Append("<br/>");

                result.Append(string.Join(' ', AddLinksToWords(trimmed)));
                addNewLine = true;
            }
        }

        if (listOpen)
            result.Append("</ul>");

        return result.ToString();
    }

    internal static IEnumerable<string> AddLinksToWords(string line)
    {
        foreach (string word in line.Split(' '))
        {
            string? cleanUrl = CleanHttpUrl(word);
            if (cleanUrl == null)
                yield return word;
            else yield return $"<a href=\"{word}\">{cleanUrl}</a>";
        }
    }

    internal static string? CleanHttpUrl(string url)
    {
        if (url.StartsWith("https://"))
            return "https://" + CleanHttpUrlWithoutProtocol(url.Remove(0, 8));
        else if (url.StartsWith("http://"))
            return "http://" + CleanHttpUrlWithoutProtocol(url.Remove(0, 7));
        else return null;
    }

    private static string CleanHttpUrlWithoutProtocol(string urlWithoutProto)
        => urlWithoutProto.SplitAtFirst('@', out string p1, out string p2) && !p1.Contains('/') ? p2 : urlWithoutProto;
}