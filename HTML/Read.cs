using HtmlAgilityPack;
using System.Diagnostics.CodeAnalysis;
using uwap.WebFramework.Elements;
namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    internal static List<IContent> ReadHTML(string code, bool includeImageLinks)
        => new HTMLReader(includeImageLinks).ReadHTML(code);

    private class HTMLReader(bool includeImageLinks)
    {
        private List<IContent> Result = [];
        private Paragraph? LastRealParagraph = null;
        private readonly bool IncludeImageLinks = includeImageLinks;

        public List<IContent> ReadHTML(string code)
        {
            HtmlDocument document = new();
            document.LoadHtml(code);

            ReadHTML(document.DocumentNode);

            foreach (var c in Result)
                if (c is Paragraph p)
                {
                    p.Text = p.Text.Trim();
                    if (p.Text.Replace("\xad", "") == "")
                        p.Text = "<br/>";
                }

            while (Result.Count != 0 && Result.First() is Paragraph p && p.Text == "<br/>")
                Result.RemoveAt(0);
            while (Result.Count != 0 && Result.Last() is Paragraph p && p.Text == "<br/>")
                Result.RemoveAt(Result.Count - 1);

            return Result;
        }

        private void ReadHTML(HtmlNode node)
        {
            string style = node.GetAttributeValue("style", "");
            if (style.Contains("display:none") || style.Contains("display: none"))
                return;

            switch (node.Name)
            {
                case "#comment":
                case "head":
                case "script":
                case "style":
                    //ignored
                    break;
                case "br":
                case "hr":
                    LastRealParagraph = new Paragraph("") {Unsafe = true};
                    Result.Add(LastRealParagraph);
                    break;
                case "#text":
                    { //raw text
                        string inner = node.GetDirectInnerText().Replace("\n", " ").HtmlSafe();
                        if (LastRealParagraph != null)
                        {
                            if (LastRealParagraph.Text != "" || inner.Trim() != "")
                                LastRealParagraph.Text += inner;
                        }
                        else if (inner.Trim() != "")
                        {
                            LastRealParagraph = new(inner) {Unsafe = true};
                            Result.Add(LastRealParagraph);
                        }
                    }
                    break;
                case "a":
                    { //link
                        string inner = node.InnerText.Trim();
                        string href = node.GetAttributeValue("href", "");
                        string? code = null;
                        if (href == "")
                        {
                            if (inner != "")
                                code = inner.HtmlSafe();
                        }
                        else if (IsFullHttpUrl(href, out var description) || (href.SplitAtFirst(':', out description, out _) && description != "javascript"))
                            code = $"<a href=\"{href}\" target=\"_blank\">{(inner == "" ? $"[{description}]" : $"{inner} ({description})").HtmlSafe()}</a>";

                        if (code != null)
                            if (LastRealParagraph == null)
                            {
                                LastRealParagraph = new(code) {Unsafe = true};
                                Result.Add(LastRealParagraph);
                            }
                            else LastRealParagraph.Text += code;
                    }
                    break;
                case "ul":
                    { //unordered list
                        var items = ReadItems(node.ChildNodes);
                        if (items.Count != 0)
                        {
                            LastRealParagraph = null;
                            Result.Add(new BulletList(items) {Unsafe = true});
                        }
                    }
                    break;
                case "ol":
                    { //ordered list
                        var items = ReadItems(node.ChildNodes);
                        if (items.Count != 0)
                        {
                            LastRealParagraph = null;
                            Result.Add(new OrderedList(items, node.GetAttributeValue("type", "") switch
                            {
                                "A" => OrderedList.Types.LettersUppercase,
                                "a" => OrderedList.Types.LettersLowercase,
                                "I" => OrderedList.Types.RomanNumbersUppercase,
                                "i" => OrderedList.Types.RomanNumbersLowercase,
                                _ => OrderedList.Types.Numbers
                            }) {Unsafe = true});
                        }
                    }
                    break;
                case "img":
                    { //image
                        string src = node.GetAttributeValue("src", "");
                        if (src == "")
                            break;
                        else if (IsFullHttpUrl(src, out var domain))
                        {
                            LastRealParagraph = null;
                            if (IncludeImageLinks)
                                Result.Add(new Paragraph($"<a href=\"{src}\" target=\"_blank\">[external image on {domain.HtmlSafe()} (dangerous!)]</a>") {Unsafe = true});
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
                            LastRealParagraph = null;
                            Result.Add(new Image(src, $"{height ?? maxHeight ?? $"max-height:{defMaxHeight}"};{width ?? maxWidth ?? $"max-width:{defMaxWidth}"}"));
                        }
                    }
                    break;
                case "u":
                case "i":
                case "b":
                case "s":
                    { //inline formatting
                        int oldCount = Result.Count;
                        Paragraph? lrp = LastRealParagraph;

                        if (lrp != null)
                            lrp.Text += $"<{node.Name}>";
                        foreach (var c in node.ChildNodes)
                            ReadHTML(c);
                        if (lrp != null)
                            lrp.Text += $"</{node.Name}>";

                        foreach (var c in Result.Skip(oldCount))
                            if (c is Paragraph p && p.Text.Trim().Replace("\xad", "") != "")
                                p.Text = $"<{node.Name}>{p.Text}</{node.Name}>";
                    }
                    break;
                case "#document":
                case "html":
                case "body":
                case "div":
                case "p":
                case "table":
                case "tbody":
                case "tr":
                case "th":
                case "td":
                    //independent container elements
                    LastRealParagraph = null;
                    foreach (var c in node.ChildNodes)
                        ReadHTML(c);
                    LastRealParagraph = null;
                    break;
                case "span":
                case "strong":
                case "em":
                case "li":
                default:
                    //inline containers/formatting that won't be applied, or unrecognized element that will be treated like one
                    foreach (var c in node.ChildNodes)
                        ReadHTML(c);
                    break;
            }
        }

        private List<string> ReadItems(HtmlNodeCollection children)
        {
            List<string> result = [];
            foreach (var child in children)
                if (child.Name == "li")
                {
                    var oldResult = Result;
                    Result = [];
                    LastRealParagraph = null;

                    ReadHTML(child);
                    List<string> lines = [];
                    foreach (var c in Result)
                        if (c is Paragraph p && p.Text.Trim().Replace("\xad", "") != "")
                            lines.Add(p.Text);
                    if (lines.Count != 0)
                        result.Add(string.Join(' ', lines));

                    Result = oldResult;
                    LastRealParagraph = null;
                }
            return result;
        }
    }

    private static bool IsFullHttpUrl(string url, [MaybeNullWhen(false)] out string domain)
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
}