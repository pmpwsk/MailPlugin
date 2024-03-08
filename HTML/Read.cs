﻿using HtmlAgilityPack;
using System.Diagnostics.CodeAnalysis;
using uwap.WebFramework.Elements;
namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    internal static List<IContent> ReadHTML(string code, bool includeImageLinks)
    {
        HtmlDocument document = new();
        document.LoadHtml(code);
        List<IContent> result = [];
        foreach (var c in ReadHTML(document.DocumentNode, includeImageLinks))
            if (c != null)
                result.Add(c);
        while (result.Count != 0 && result.First() is Paragraph p && (p.Text == "" || p.Text == "<br/>"))
            result.RemoveAt(0);
        while (result.Count != 0 && result.Last() is Paragraph p && (p.Text == "" || p.Text == "<br/>"))
            result.RemoveAt(result.Count - 1);
        return result;
    }

    private static IEnumerable<IContent?> ReadHTML(HtmlNode node, bool includeImageLinks, bool trimText = true)
    {
        string style = node.GetAttributeValue("style", "");
        if (style.Contains("display:none") || style.Contains("display: none"))
            yield break;

        switch (node.Name)
        {
            case "#comment":
            case "head":
            case "script":
            case "style":
                //ignored
                break;
            case "#document":
            case "html":
            case "body":
            case "div":
            case "p":
                //independent container elements
                yield return null;
                foreach (var c in ReadHTMLChildren(node.ChildNodes, includeImageLinks, false))
                    yield return c;
                yield return null;
                break;
            case "#text":
                { //raw text
                    string inner = node.GetDirectInnerText();
                    if (trimText)
                        inner = inner.Trim();
                    if (inner == "")
                        break;
                    yield return new Paragraph(inner.Replace("\n", " ").HtmlSafe());
                }
                break;
            case "u":
            case "i":
            case "b":
            case "s":
                //inline formatting
                foreach (var c in ReadHTMLChildren(node.ChildNodes, includeImageLinks, false))
                    if (c is Paragraph p)
                    {
                        if (p.Text != "<br/>")
                            p.Text = $"<{node.Name}>{p.Text}</{node.Name}>";
                        yield return p;
                    }
                    else yield return c;
                break;
            case "a":
                { //link
                    string inner = node.InnerText.Trim();
                    string href = node.GetAttributeValue("href", "");
                    if (href == "")
                    {
                        if (inner != "")
                            yield return new Paragraph(inner.HtmlSafe());
                    }
                    else if (IsFullHttpUrl(href, out var description) || (href.SplitAtFirst(':', out description, out _) && description != "javascript"))
                        yield return new Paragraph($"<a href=\"{href}\" target=\"_blank\">{(inner == "" ? $"[{description}]" : $"{inner} ({description})").HtmlSafe()}</a>");
                }
                break;
            case "ul":
                { //unordered list
                    var items = ReadItems(node.ChildNodes, includeImageLinks);
                    if (items.Count != 0)
                        yield return new BulletList(items);
                }
                break;
            case "ol":
                { //ordered list
                    var items = ReadItems(node.ChildNodes, includeImageLinks);
                    if (items.Count != 0)
                        yield return new OrderedList(items, node.GetAttributeValue("type", "") switch
                        {
                            "A" => OrderedList.Types.LettersUppercase,
                            "a" => OrderedList.Types.LettersLowercase,
                            "I" => OrderedList.Types.RomanNumbersUppercase,
                            "i" => OrderedList.Types.RomanNumbersLowercase,
                            _ => OrderedList.Types.Numbers
                        });
                }
                break;
            case "img":
                { //image
                    string src = node.GetAttributeValue("src", "");
                    if (src == "")
                        break;
                    else if (IsFullHttpUrl(src, out var domain))
                    {
                        yield return null;
                        if (includeImageLinks)
                        {
                            yield return new Paragraph($"<a href=\"{src}\" target=\"_blank\">[external image on {domain.HtmlSafe()} (dangerous!)]</a>");
                            yield return null;
                        }
                    }
                    else if (src.StartsWith("data:image/"))
                    {
                        string defMaxHeight = "5rem";
                        string defMaxWidth = "calc(100% - 0.05rem)";
                        string? height = null;
                        string? width = null;
                        string? maxHeight = null;
                        string? maxWidth = null;
                        foreach (var attribute in node.GetAttributeValue("style", "").Split(';').Select(x => x.Trim().ToLower()).Where(x => x != ""))
                        {
                            if (!attribute.SplitAtFirst(':', out var key, out var value))
                                continue;
                            key = key.TrimEnd();
                            value = value.TrimStart();
                            string? unit = null;
                            switch (key)
                            {
                                case "height":
                                case "width":
                                case "max-height":
                                case "max-width":
                                    break;
                                default:
                                    continue;
                            }
                            foreach (var u in new[] { "cm", "mm", "in", "px", "pt", "pc", "rem", "em", "ex", "ch", "vw", "vh", "vmin", "vmax", "%" })
                                if (value.EndsWith(u))
                                {
                                    unit = u;
                                    value = value[..^u.Length];
                                    break;
                                }
                            if (!double.TryParse(value, out var valueWithoutUnit))
                                continue;
                            unit ??= "px";
                            switch (key)
                            {
                                case "height":
                                    height = $"height:{valueWithoutUnit}{unit}";
                                    break;
                                case "width":
                                    width = $"width:min({defMaxWidth},{valueWithoutUnit}{unit})";
                                    break;
                                case "max-height":
                                    maxHeight = $"max-height:{valueWithoutUnit}{unit}";
                                    break;
                                case "max-width":
                                    maxWidth = $"max-width:min({defMaxWidth},{valueWithoutUnit}{unit})";
                                    break;
                            }
                        }
                        yield return new Image(src, $"{height ?? maxHeight ?? $"max-height:{defMaxHeight}"};{width ?? maxWidth ?? $"max-width:{defMaxWidth}"}");
                    }
                }
                break;
            case "table":
            case "tbody":
                { //table
                    yield return null;
                    foreach (var child in node.ChildNodes)
                        if (child.Name == "tbody")
                        {
                            foreach (var c in ReadHTML(child, includeImageLinks))
                                yield return c;
                        }
                        else if (child.Name == "tr")
                        {
                            foreach (var childchild in child.ChildNodes)
                                if (childchild.Name == "td" || childchild.Name == "th")
                                {
                                    yield return null;
                                    foreach (var c in ReadHTMLChildren(childchild.ChildNodes, includeImageLinks, false))
                                        yield return c;
                                    yield return null;
                                }
                        }
                    yield return null;
                }
                break;
            case "span":
            case "strong":
            case "em":
            default:
                //inline containers/formatting that won't be applied, or unrecognized element that will be treated like one
                foreach (var c in ReadHTMLChildren(node.ChildNodes, includeImageLinks, false))
                    yield return c;
                break;
        }
    }

    private static IEnumerable<IContent?> ReadHTMLChildren(HtmlNodeCollection children, bool includeImageLinks, bool inlineOnly)
    {
        string? buffer = null;
        foreach (var child in children)
        {
            if (child.Name == "br" || child.Name == "hr")
            {
                if (buffer != null)
                {
                    yield return null;
                    yield return new Paragraph(LineBreakIfEmpty(buffer.TrimEnd()));
                    yield return null;
                }
                buffer = "";
                continue;
            }

            foreach (var c in ReadHTML(child, includeImageLinks, buffer == null))
                if (c != null && c is Paragraph p)
                {
                    if (buffer == null)
                        buffer = p.Text.TrimStart();
                    else buffer += p.Text;
                }
                else if (!inlineOnly)
                {
                    if (buffer != null)
                    {
                        yield return null;
                        yield return new Paragraph(LineBreakIfEmpty(buffer.TrimEnd()));
                        yield return null;
                        buffer = null;
                    }
                    yield return c;
                }
        }

        if (buffer != null)
        {
            yield return new Paragraph(LineBreakIfEmpty(buffer.TrimEnd()));
            if (buffer == "")
                yield return null;
        }
    }

    private static List<string> ReadItems(HtmlNodeCollection children, bool includeImageLinks)
    {
        List<string> result = [];
        foreach (var child in children)
            if (child.Name == "li")
            {
                List<string> lines = [];
                foreach (var c in ReadHTMLChildren(child.ChildNodes, includeImageLinks, true))
                    if (c is Paragraph p)
                        lines.Add(p.Text);
                if (lines.Count != 0)
                    result.Add(string.Join(' ', lines));
            }
        return result;
    }

    public static bool IsFullHttpUrl(string url, [MaybeNullWhen(false)] out string domain)
    {
        domain = null;

        if (url.StartsWith("https://"))
            url = url.Remove(0, 8);
        else if (url.StartsWith("http://"))
            url = url.Remove(0, 7);
        else return false;

        domain = url.Before('/').After('@');
        return true;
    }

    private static string LineBreakIfEmpty(string text)
        => text.Replace("\xad", "") == "" ? "<br/>" : text;
}