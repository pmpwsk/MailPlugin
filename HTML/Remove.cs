using HtmlAgilityPack;
using uwap.WebFramework.Elements;
namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    internal static string RemoveHTML(string code)
        => new HTMLRemover().Read(code);

    private class HTMLRemover
    {
        private List<IContent> Result = [];
        private Paragraph? LastRealParagraph = null;

        public string Read(string code)
        {
            HtmlDocument document = new();
            document.LoadHtml(code);

            Read(document.DocumentNode);

            foreach (var c in Result)
                if (c is Paragraph p)
                {
                    p.Text = p.Text.Trim();
                    if (p.Text.Replace("\xad", "") == "")
                        p.Text = "";
                }

            while (Result.Count != 0 && Result.First() is Paragraph p && p.Text == "")
                Result.RemoveAt(0);
            while (Result.Count != 0 && Result.Last() is Paragraph p && p.Text == "")
                Result.RemoveAt(Result.Count - 1);

            List<string> result = [];
            foreach (var c in Result)
                switch (c)
                {
                    case Paragraph p:
                        result.Add(p.Text);
                        break;
                    case BulletList ul:
                        foreach (var item in ul.List)
                            result.Add("- " + item);
                        break;
                    case OrderedList ol:
                        int i = 1;
                        foreach (var item in ol.List)
                        {
                            result.Add(i + ". " + item);
                            i++;
                        }
                        break;
                }
            return string.Join('\n', result);
        }

        private void Read(HtmlNode node)
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
                    LastRealParagraph = new Paragraph("");
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
                            LastRealParagraph = new(inner);
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
                                LastRealParagraph = new(code);
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
                            Result.Add(new BulletList(items));
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
                            }));
                        }
                    }
                    break;
                case "img":
                    { //image
                        string src = node.GetAttributeValue("src", "");
                        if (src == "")
                            break;
                        else if (IsFullHttpUrl(src, out _))
                        {
                            LastRealParagraph = null;
                            Result.Add(new Paragraph($"[external image]({src})"));
                        }
                        else if (src.StartsWith("data:image/"))
                        {
                            LastRealParagraph = null;
                            Result.Add(new Paragraph("[image]"));
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
                            Read(c);
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
                        Read(c);
                    LastRealParagraph = null;
                    break;
                case "span":
                case "strong":
                case "em":
                case "li":
                default:
                    //inline containers/formatting that won't be applied, or unrecognized element that will be treated like one
                    foreach (var c in node.ChildNodes)
                        Read(c);
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

                    Read(child);
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
}