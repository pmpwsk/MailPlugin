﻿using HtmlAgilityPack;
using uwap.WebFramework.Elements;
namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    internal static string RemoveHTML(string code)
    {
        HtmlDocument document = new();
        document.LoadHtml(code);
        List<string> result = new();
        foreach (var c in RemoveHTML(document.DocumentNode))
        {
            if (c == null)
                continue;

            if (c is Paragraph p)
            {
                if (p.Text == "<br/>")
                    p.Text = "";
                result.Add(p.Text);
            }
            else if (c is BulletList ul)
            {
                foreach (var item in ul.List)
                {
                    result.Add("- " + item);
                }
            }
            else if (c is OrderedList ol)
            {
                int i = 0;
                foreach (var item in ol.List)
                {
                    result.Add(i + ". " + item);
                    i++;
                }
            }
        }
        return string.Join('\n', result);
    }

    private static IEnumerable<IContent?> RemoveHTML(HtmlNode node, bool trimText = true)
    {
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
                foreach (var c in RemoveHTMLChildren(node.ChildNodes, false))
                    yield return c;
                yield return null;
                break;
            case "#text":
                { //raw text
                    string inner = node.GetDirectInnerText();
                    if (trimText)
                    {
                        string trimmed = inner.Trim();
                        if (trimmed == "")
                            break;
                    }
                    yield return new Paragraph(inner.Replace("\n", " ").HtmlSafe());
                }
                break;
            case "u":
            case "i":
            case "b":
                //inline formatting
                foreach (var c in RemoveHTMLChildren(node.ChildNodes, false))
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
                    else
                    {
                        if (inner == "")
                            inner = "[link without text]";
                        if (IsFullHttpUrl(href, out var description) || (href.SplitAtFirst(':', out description, out _) && description != "javascript"))
                            yield return new Paragraph($"<a href=\"{href}\" target=\"_blank\">{inner.HtmlSafe()} ({description})</a>");
                    }
                }
                break;
            case "ul":
                { //unordered list
                    var items = RemoveHTMLItems(node.ChildNodes);
                    if (items.Any())
                        yield return new BulletList(items);
                }
                break;
            case "ol":
                { //ordered list
                    var items = RemoveHTMLItems(node.ChildNodes);
                    if (items.Any())
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
                        yield return new Paragraph($"<a href=\"{src}\" target=\"_blank\">[external image on {domain} (dangerous!)]</a>");
                        yield return null;
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
                            if (!new[] { "height", "width", "max-height", "max-width" }.Contains(key))
                                continue;
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
                            foreach (var c in RemoveHTML(child))
                                yield return c;
                        }
                        else if (child.Name == "tr")
                        {
                            foreach (var childchild in child.ChildNodes)
                                if (childchild.Name == "td")
                                {
                                    yield return null;
                                    foreach (var c in RemoveHTMLChildren(childchild.ChildNodes, false))
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
                foreach (var c in RemoveHTMLChildren(node.ChildNodes, false))
                    yield return c;
                break;
        }
    }

    private static IEnumerable<IContent?> RemoveHTMLChildren(HtmlNodeCollection children, bool inlineOnly)
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

            foreach (var c in RemoveHTML(child, buffer == null))
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

    private static List<string> RemoveHTMLItems(HtmlNodeCollection children)
    {
        List<string> result = new();
        foreach (var child in children)
            if (child.Name == "li")
            {
                List<string> lines = new();
                foreach (var c in RemoveHTMLChildren(child.ChildNodes, true))
                {
                    if (c is Paragraph p)
                        lines.Add(p.Text);
                }
                if (lines.Any())
                    result.Add(string.Join(' ', lines));
            }
        return result;
    }
}