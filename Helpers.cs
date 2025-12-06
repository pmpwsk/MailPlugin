using System.Web;
using uwap.WebFramework.Elements;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    private static void CreatePage(Request req, string title, out Page page, out List<IPageElement> e)
    {
        req.ForceGET();
        Presets.CreatePage(req, title, out page, out e);
        req.ForceLogin();
        page.Head.Add($"<link rel=\"manifest\" href=\"{req.PluginPathPrefix}/manifest.json\" />");
        page.Favicon = $"{req.PluginPathPrefix}/icon.ico";
        page.Navigation =
        [
            page.Navigation.Count != 0 ? page.Navigation.First() : new Button(req.Domain, "/"),
            new Button("Mail", $"{req.PluginPathPrefix}/")
        ];
    }

    private static void POST(Request req)
    {
        req.ForcePOST();
        req.ForceLogin(false);
    }

    private static void GET(Request req)
    {
        req.ForceGET();
        req.ForceLogin(false);
    }

    private static ulong LastInboxMessageId(Mailbox mailbox)
    {
        if (mailbox.Folders.TryGetValue("Inbox", out var inbox) && inbox.Count != 0)
            return inbox.Max;
        else return 0;
    }

    private static ulong LastUnreadInboxMessageId(Mailbox mailbox, ulong after)
    {
        if (mailbox.Folders.TryGetValue("Inbox", out var inbox))
            foreach (var id in inbox.Reverse())
                if (id < after)
                    return 0;
                else if (mailbox.Messages.TryGetValue(id, out var message) && message.Unread)
                    return id;
        return 0;
    }

    private static CustomScript IncomingScript(Request req, ulong last)
    {
        string query = req.QueryString;
        if (query == "")
            query = "?";
        else query += "&";
        query += $"last={last}";
        return new CustomScript($"let incomingEvent = new EventSource('{req.PluginPathPrefix}/incoming-event{query}');\nonbeforeunload = (event) => {{ incomingEvent.close(); }};\n\nincomingEvent.onmessage = function (event) {{ switch (event.data) {{ case 'refresh': window.location.reload(); break; case 'icon': if (!document.querySelector(\"link[rel~='icon']\").href.includes('red')) {{ document.querySelector(\"link[rel~='icon']\").href = '{req.PluginPathPrefix}/icon-red.ico'; }} break; }} }};");
    }

    private static string PathWithoutQueries(string path, Request req, params string[] queries)
    {
        string query = string.Join('&', req.Query.ListAll().Where(x => !queries.Contains(x.Key)).Select(x => $"{x.Key}={(x.Key == "folder" ? HttpUtility.UrlEncode(x.Value) : x.Value)}"));
        if (query == "")
            return path;
        else return $"{path}?{query}";
    }

    private static void HighlightSidebar(string currentPath, Page page, Request req, params string[] ignoredQueries)
    {
        string url = PathWithoutQueries(currentPath, req, ignoredQueries);
        foreach (IPageElement element in page.Sidebar)
            if (element is ButtonElement button && button.Link == url)
                button.Class = "green";
    }
}