using HtmlAgilityPack;
namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    internal static string QuoteHTML(string code)
    {
        HtmlDocument document = new();
        document.LoadHtml(code);
        List<string> result = [];
        foreach (var c in QuoteHTML(document.DocumentNode))
            if (c != null)
                result.Add(c);
        while (result.Count != 0 && result.First() == "")
            result.RemoveAt(0);
        while (result.Count != 0 && result.Last() == "")
            result.RemoveAt(result.Count - 1);
        return string.Join('\n', result);
    }

    private static IEnumerable<string?> QuoteHTML(HtmlNode node, bool trimText = true)
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
                foreach (var c in QuoteHTMLChildren(node.ChildNodes, false))
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
                    yield return inner.Replace("\n", " ");
                }
                break;
            case "u":
            case "i":
            case "b":
                //inline formatting
                foreach (var c in QuoteHTMLChildren(node.ChildNodes, false))
                    if (c != null)
                    {
                        if (c != "")
                            yield return $"<{node.Name}>{c}</{node.Name}>";
                        else yield return "";
                    }
                    else yield return null;
                break;
            case "a":
                { //link
                    string inner = node.InnerText.Trim();
                    string href = node.GetAttributeValue("href", "");
                    if (href == "")
                    {
                        if (inner != "")
                            yield return inner;
                    }
                    else
                    {
                        if (inner == "")
                            inner = "[link without text]";
                        if (IsFullHttpUrl(href, out _) || (href.SplitAtFirst(':', out var description, out _) && description != "javascript"))
                            yield return $"<a href=\"{href}\">{inner})</a>";
                    }
                }
                break;
            case "ul":
                { //unordered list
                    foreach (var item in QuoteItems(node.ChildNodes))
                        yield return "- " + item;
                }
                break;
            case "ol":
                { //ordered list
                    var items = QuoteItems(node.ChildNodes);
                    if (items.Count != 0)
                    {
                        string? type = node.GetAttributeValue("type", "");
                        switch (type)
                        {
                            case "A":
                            case "a":
                            case "I":
                            case "i":
                                break;
                            default:
                                type = null;
                                break;
                        }
                        yield return $"<ol{(type == null ? "" : $" type=\"{type}\"")}>{string.Join("", items.Select(x => $"<li>{x}</li>"))}</ol>";
                    }
                }
                break;
            case "img":
                { //image
                    yield return null;
                }
                break;
            case "table":
            case "tbody":
                { //table
                    yield return null;
                    foreach (var child in node.ChildNodes)
                        if (child.Name == "tbody")
                        {
                            foreach (var c in QuoteHTML(child))
                                yield return c;
                        }
                        else if (child.Name == "tr")
                        {
                            foreach (var childchild in child.ChildNodes)
                                if (childchild.Name == "td")
                                {
                                    yield return null;
                                    foreach (var c in QuoteHTMLChildren(childchild.ChildNodes, false))
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
                foreach (var c in QuoteHTMLChildren(node.ChildNodes, false))
                    yield return c;
                break;
        }
    }

    private static IEnumerable<string?> QuoteHTMLChildren(HtmlNodeCollection children, bool inlineOnly)
    {
        string? buffer = null;
        foreach (var child in children)
        {
            if (child.Name == "br" || child.Name == "hr")
            {
                if (buffer != null)
                {
                    yield return null;
                    yield return buffer.TrimEnd();
                    yield return null;
                }
                buffer = "";
                continue;
            }

            foreach (var c in QuoteHTML(child, buffer == null))
                if (c != null)
                {
                    if (buffer == null)
                        buffer = c.TrimStart();
                    else buffer += c;
                }
                else if (!inlineOnly)
                {
                    if (buffer != null)
                    {
                        yield return null;
                        yield return buffer.TrimEnd();
                        yield return null;
                        buffer = null;
                    }
                    yield return c;
                }
        }

        if (buffer != null)
        {
            yield return buffer.TrimEnd();
            if (buffer == "")
                yield return null;
        }
    }

    private static List<string> QuoteItems(HtmlNodeCollection children)
    {
        List<string> result = [];
        foreach (var child in children)
            if (child.Name == "li")
            {
                List<string> lines = [];
                foreach (var c in QuoteHTMLChildren(child.ChildNodes, true))
                    if (c != null)
                        lines.Add(c);
                if (lines.Count != 0)
                    result.Add(string.Join(' ', lines));
            }
        return result;
    }
}