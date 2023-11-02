﻿using System.Text;
namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    internal static string AddHTML(string text)
    {
        string[] lines = text.Replace("\r", "").Trim().Split('\n');
        if (lines.Length == 0 || (lines.Length == 1 && lines[0] == ""))
            return "";

        StringBuilder result = new();
        bool listOpen = false;
        bool addNewLine = false;
        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith('-'))
            {
                if (!listOpen)
                {
                    result.Append("<ul>");
                    listOpen = true;
                }
                result.Append("<li>" + trimmed[1..].TrimStart() + "</li>");
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
                result.Append(line.Trim());
                addNewLine = true;
            }
        }

        if (listOpen)
            result.Append("</ul>");

        return result.ToString();
    }
}