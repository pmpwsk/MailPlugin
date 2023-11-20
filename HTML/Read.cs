using HtmlAgilityPack;
using System.Diagnostics.CodeAnalysis;
using uwap.WebFramework.Elements;
namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    internal static List<IContent> ReadHTML(string code)
    {
        HtmlDocument document = new();
        document.LoadHtml(code);
        List<IContent> result = [];
        foreach (var c in ReadHTML(document.DocumentNode))
            if (c != null)
                result.Add(c);
        while (result.Count != 0 && result.First() is Paragraph p && (p.Text == "" || p.Text == "<br/>"))
            result.RemoveAt(0);
        while (result.Count != 0 && result.Last() is Paragraph p && (p.Text == "" || p.Text == "<br/>"))
            result.RemoveAt(result.Count - 1);
        return result;
    }

    private static IEnumerable<IContent?> ReadHTML(HtmlNode node, bool trimText = true)
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
                foreach (var c in ReadHTMLChildren(node.ChildNodes, false))
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
                foreach (var c in ReadHTMLChildren(node.ChildNodes, false))
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
                    var items = ReadItems(node.ChildNodes);
                    if (items.Count != 0)
                        yield return new BulletList(items);
                }
                break;
            case "ol":
                { //ordered list
                    var items = ReadItems(node.ChildNodes);
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
                            foreach (var c in ReadHTML(child))
                                yield return c;
                        }
                        else if (child.Name == "tr")
                        {
                            foreach (var childchild in child.ChildNodes)
                                if (childchild.Name == "td")
                                {
                                    yield return null;
                                    foreach (var c in ReadHTMLChildren(childchild.ChildNodes, false))
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
                foreach (var c in ReadHTMLChildren(node.ChildNodes, false))
                    yield return c;
                break;
        }
    }

    private static IEnumerable<IContent?> ReadHTMLChildren(HtmlNodeCollection children, bool inlineOnly)
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

            foreach (var c in ReadHTML(child, buffer == null))
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

    private static List<string> ReadItems(HtmlNodeCollection children)
    {
        List<string> result = [];
        foreach (var child in children)
            if (child.Name == "li")
            {
                List<string> lines = [];
                foreach (var c in ReadHTMLChildren(child.ChildNodes, true))
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

        if (url.SplitAtLast('@', out _, out var newUrl))
            url = newUrl;

        domain = url.Before('/');
        return true;
    }

    private static string LineBreakIfEmpty(string text)
        => text == "" ? "<br/>" : text;
}