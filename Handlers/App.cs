﻿using System.Web;
using uwap.WebFramework.Accounts;
using uwap.WebFramework.Elements;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private ulong LastInboxMessageId(Mailbox mailbox)
    {
        if (mailbox.Folders.TryGetValue("Inbox", out var inbox) && inbox.Any())
            return inbox.Max;
        else return 0;
    }

    private IScript IncomingScript(AppRequest req, ulong last, string pathPrefix)
    {
        string query = req.Context.Request.QueryString.HasValue ? req.Context.Request.QueryString.Value ?? "" : "";
        if (query == "")
            query = "?";
        else query += "&";
        query += $"last={last}";
        return new CustomScript($"let incomingEvent = new EventSource('/event{pathPrefix}/incoming{query}');\nonbeforeunload = (event) => {{ incomingEvent.close(); }};\n\nincomingEvent.onmessage = function (event) {{ switch (event.data) {{ case 'refresh': window.location.reload(); break; case 'icon': if (!document.querySelector(\"link[rel~='icon']\").href.includes('red')) {{ document.querySelector(\"link[rel~='icon']\").href = '{pathPrefix}/icon-red.ico'; }} break; }} }};");
    }

    public override Task Handle(AppRequest req, string path, string pathPrefix)
    {
        Presets.CreatePage(req, "Mail", out Page page, out List<IPageElement> e);
        Presets.Navigation(req, page);
        if (req.User == null || (!req.LoggedIn))
        {
            e.Add(new HeadingElement("Not logged in!", "You need to be logged in to use this application.", "red"));
            return Task.CompletedTask;
        }

        string pluginHome = pathPrefix == "" ? "/" : pathPrefix;

        page.Favicon = pathPrefix + "/icon.ico";
        page.Head.Add($"<link rel=\"manifest\" href=\"{pathPrefix}/manifest.json\" />");
        page.Navigation = new() { page.Navigation.FirstOrDefault() ?? new Button(req.Domain, "/"), new Button("Mail", pluginHome) };
        switch (path)
        {
            case "":
                {
                    page.Scripts.Add(new CustomScript($"document.cookie = \"TimeOffset=\" + new Date().getTimezoneOffset() + \"; domain={req.Domain}; path=/; SameSite=Strict\""));
                    if (!req.Query.TryGetValue("mailbox", out string? mailboxId))
                    {/////
                        //list mailboxes that the user has access to or an error if none were found, redirect to mailbox if only one is present and user isn't admin
                        page.Navigation.Add(new Button("Back", "/", "right"));
                        page.Title = "Mailboxes";
                        bool isAdmin = req.IsAdmin();
                        var mailboxes = (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : new HashSet<Mailbox>())
                            .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@'));

                        if (isAdmin || mailboxes.Count() != 1)
                        {
                            e.Add(new LargeContainerElement("Mailboxes", ""));
                            if (isAdmin)
                            {
                                e.Add(new ButtonElement("Manage mailboxes", "", pathPrefix + "/manage"));
                            }
                            if (mailboxes.Any())
                            {
                                page.Scripts.Add(IncomingScript(req, mailboxes.Max(LastInboxMessageId), pathPrefix));
                                bool anyUnread = false;
                                foreach (Mailbox m in mailboxes)
                                {
                                    bool unread = GetLastReversed(m.Folders["Inbox"], MessagePreloadCount, 0).Any(x => m.Messages.TryGetValue(x, out var message) && message.Unread);
                                    if (unread) anyUnread = true;
                                    e.Add(new ButtonElement((unread ? "(!) " : "") + m.Address, m.Name ?? "", $"{pluginHome}?mailbox={m.Id}", unread ? "red" : null));
                                }
                                if (anyUnread)
                                    page.Favicon = pathPrefix + "/icon-red.ico";
                            }
                            else
                            {
                                e.Add(new ContainerElement("No mailboxes!", "If you'd like to get your own mailbox, please contact the support.", "red"));
                                Presets.AddSupportButton(page);
                            }
                        }
                        else req.Redirect($"{pluginHome}?mailbox={mailboxes.First().Id}");
                        break;
                    }
                    if (!Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                    {
                        //mailbox doesn't exist
                        page.Navigation.Add(new Button("Back", pluginHome, "right"));
                        e.Add(new LargeContainerElement("Error", "This mailbox doesn't exist!", "red"));
                        break;
                    }
                    if ((!mailbox.AllowedUserIds.TryGetValue(req.UserTable.Name, out var allowedUserIds)) || !allowedUserIds.Contains(req.User.Id))
                    {
                        //user doesn't have access to this mailbox
                        page.Navigation.Add(new Button("Back", pluginHome, "right"));
                        e.Add(new LargeContainerElement("Error", "You don't have access to this mailbox!", "red"));
                        break;
                    }

                    if (!req.Query.TryGetValue("folder", out var folderName))
                    {/////
                        //list folders in the mailbox (inbox, sent, recycle bin, spam are pinned)
                        var mailboxes = (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : new HashSet<Mailbox>())
                            .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')).ToList();
                        page.Navigation.Add(new Button("Back", mailboxes.Count == 1 && !req.IsAdmin() ? "/" : pluginHome, "right"));
                        page.Scripts.Add(IncomingScript(req, LastInboxMessageId(mailbox), pathPrefix));
                        page.Title = $"Mail ({mailbox.Address})";
                        page.Sidebar.Add(new ButtonElement("Mailboxes:", null, $"{pluginHome}"));
                        bool anyUnread = false;
                        foreach (Mailbox m in mailboxes)
                        {
                            bool unread = GetLastReversed(m.Folders["Inbox"], MessagePreloadCount, 0).Any(x => m.Messages.TryGetValue(x, out var message) && message.Unread);
                            if (unread) anyUnread = true;
                            page.Sidebar.Add(new ButtonElement(null, (unread ? "(!) " : "") + m.Address, $"{pluginHome}?mailbox={m.Id}", unread ? "red" : null));
                        }
                        HighlightSidebar(page, req);
                        e.Add(new LargeContainerElement($"Mail ({mailbox.Address})", "", "overflow"));
                        e.Add(new ContainerElement(null, "Actions:") { Buttons = new()
                        {
                            new Button("Settings", $"{pathPrefix}/settings?mailbox={mailboxId}"),
                            new Button("Send", $"{pathPrefix}/send?mailbox={mailboxId}", "green"),
                        }
                        });
                        foreach (var folderItem in SortFolders(mailbox.Folders))
                        {
                            bool unread = GetLastReversed(folderItem.Value, MessagePreloadCount, 0).Any(x => mailbox.Messages.TryGetValue(x, out var message) && message.Unread);
                            if (unread) anyUnread = true;
                            e.Add(new ButtonElement((unread ? "(!) " : "") + folderItem.Key, CountString(folderItem.Value.Count, "message"), $"{pluginHome}?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderItem.Key)}", unread ? "red" : null));
                        }
                        if (anyUnread)
                            page.Favicon = pathPrefix + "/icon-red.ico";
                        break;
                    }
                    if (!mailbox.Folders.TryGetValue(folderName, out var folder))
                    {
                        //folder doesn't exist
                        page.Navigation.Add(new Button("Back", pluginHome, "right"));
                        e.Add(new LargeContainerElement("Error", "This folder doesn't exist!", "red"));
                        break;
                    }

                    if (!req.Query.TryGetValue("message", out var messageIdString))
                    {/////
                        //list n messages (with offset) in the folder
                        page.Navigation.Add(new Button("Back", $"{pluginHome}?mailbox={mailboxId}", "right"));
                        page.Title = $"{folderName} ({mailbox.Address})";
                        page.Scripts.Add(IncomingScript(req, LastInboxMessageId(mailbox), pathPrefix));
                        page.Sidebar.Add(new ButtonElement("Folders:", null, $"{pluginHome}?mailbox={mailboxId}"));
                        bool anyUnread = false;
                        foreach (var folderItem in SortFolders(mailbox.Folders))
                        {
                            bool unread = GetLastReversed(folderItem.Value, MessagePreloadCount, 0).Any(x => mailbox.Messages.TryGetValue(x, out var message) && message.Unread);
                            if (unread) anyUnread = true;
                            page.Sidebar.Add(new ButtonElement(null, (unread ? "(!) " : "") + folderItem.Key, $"{pluginHome}?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderItem.Key)}", unread ? "red" : null));
                        }
                        if (anyUnread)
                            page.Favicon = pathPrefix + "/icon-red.ico";
                        HighlightSidebar(page, req);
                        e.Add(new LargeContainerElement($"{folderName} ({mailbox.Address})", ""));
                        if (folder.Any())
                        {
                            int offset = 0;
                            if (req.Query.TryGetValue("offset", out var offsetString) && int.TryParse(offsetString, out int offset2) && offset2 >= 0)
                                offset = offset2;
                            if (offset > 0)
                                e.Add(new ButtonElement(null, "Newer messages", $"{PathWithoutQueries(req, "offset")}&offset={Math.Max(offset - MessagePreloadCount, 0)}"));
                            string offsetQuery = offset == 0 ? "" : $"&offset={offset}";
                            foreach (var mId in GetLastReversed(folder, MessagePreloadCount, offset))
                                if (mailbox.Messages.TryGetValue(mId, out var m))
                                    e.Add(new ButtonElement(m.Subject, m.From.FullString + "<br/>" + DateTimeString(AdjustDateTime(req, m.TimestampUtc)), $"{pluginHome}?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderName)}&message={mId}{offsetQuery}", m.Unread ? "red" : null));
                            if (offset + MessagePreloadCount < folder.Count)
                                e.Add(new ButtonElement(null, "Older messages", $"{PathWithoutQueries(req, "offset")}&offset={offset + MessagePreloadCount}"));
                        }
                        else
                        {
                            e.Add(new ContainerElement("No messages!", "", "red"));
                        }
                        break;
                    }
                    if ((!ulong.TryParse(messageIdString, out ulong messageId)) || messageId == 0 || !mailbox.Messages.TryGetValue(messageId, out var message))
                    {
                        //message id isn't valid or doesn't exist
                        page.Navigation.Add(new Button("Back", pluginHome, "right"));
                        e.Add(new LargeContainerElement("Error", "This message doesn't exist!", "red"));
                        break;
                    }
                    if (!folder.Contains(messageId))
                    {
                        //message isn't part of the folder, try to find the new folder and redirect
                        req.Redirect($"{pluginHome}?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(mailbox.Folders.Where(x => x.Value.Contains(messageId)).First().Key)}&message={messageIdString}");
                        break;
                    }
                    else
                    {/////
                        //show message in a certain way (another query)
                        if (message.Unread)
                        {
                            mailbox.Lock();
                            message.Unread = false;
                            mailbox.UnlockSave();
                        }
                        string messagePath = $"../Mail/{mailboxId}/{messageId}/";
                        if (req.Query.TryGetValue("view", out var view))
                        {
                            switch (view)
                            {
                                case "text":
                                    {
                                        string code = File.Exists(messagePath + "text") ? File.ReadAllText(messagePath + "text").HtmlSafe().Replace("\n", "<br/>") : "No text attached!";
                                        req.Page = new RawHtmlCodePage(code);
                                    } break;
                                case "html":
                                    {
                                        string code = File.Exists(messagePath + "html") ? File.ReadAllText(messagePath + "html").HtmlSafe().Replace("\n", "<br/>") : "No HTML attached!";
                                        req.Page = new RawHtmlCodePage(code);
                                    } break;
                                case "load-html":
                                    req.Page = new RawHtmlFilePage($"../Mail/{mailboxId}/{messageId}/html");
                                    break;
                                default:
                                    req.Status = 400;
                                    break;
                            }
                            return Task.CompletedTask;
                        }

                        bool hasText = File.Exists(messagePath + "text");
                        bool hasHtml = File.Exists(messagePath + "html");

                        page.Navigation.Add(new Button("Back", PathWithoutQueries(req, "message", "view", "offset"), "right"));
                        page.Title = $"{message.Subject} ({mailbox.Address})";
                        page.Scripts.Add(new Script(pathPrefix + "/query.js"));
                        page.Scripts.Add(new Script(pathPrefix + "/message.js"));
                        page.Sidebar.Add(new ButtonElement("Messages:", null, PathWithoutQueries(req, "message", "view", "offset")));
                        int offset = 0;
                        if (req.Query.TryGetValue("offset", out var offsetString) && int.TryParse(offsetString, out int offset2) && offset2 >= 0)
                            offset = offset2;
                        if (offset > 0)
                            page.Sidebar.Add(new ButtonElement(null, "Newer messages", $"{PathWithoutQueries(req, "offset")}&offset={Math.Max(offset - MessagePreloadCount, 0)}"));
                        string offsetQuery = offset == 0 ? "" : $"&offset={offset}";
                        foreach (var mId in GetLastReversed(folder, MessagePreloadCount, offset))
                            if (mailbox.Messages.TryGetValue(mId, out var m))
                                page.Sidebar.Add(new ButtonElement(null, $"{m.From.FullString}:<br/>{m.Subject}", $"{pluginHome}?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderName)}&message={mId}{offsetQuery}", m.Unread ? "red" : null));
                        if (offset + MessagePreloadCount < folder.Count)
                            page.Sidebar.Add(new ButtonElement(null, "Older messages", $"{PathWithoutQueries(req, "offset")}&offset={offset + MessagePreloadCount}"));
                        HighlightSidebar(page, req, "view");
                        List<IContent> headingContents = new();
                        if (message.InReplyToId != null)
                            headingContents.Add(new Paragraph($"This is a reply to another email (<a href=\"javascript:\" onclick=\"FindOriginal('{HttpUtility.UrlEncode(message.InReplyToId)}')\">find</a>)."));
                        headingContents.Add(new Paragraph(DateTimeString(AdjustDateTime(req, message.TimestampUtc))));
                        headingContents.Add(new Paragraph("From: " + message.From.FullString));
                        foreach (var to in message.To)
                            headingContents.Add(new Paragraph("To: " + to.FullString));
                        foreach (var cc in message.Cc)
                            headingContents.Add(new Paragraph("CC: " + cc.FullString));
                        foreach (var bcc in message.Bcc)
                            headingContents.Add(new Paragraph("BCC: " + bcc.FullString));
                        e.Add(new LargeContainerElement($"{message.Subject}", headingContents) { Button = new ButtonJS("Delete", "Delete()", "red", id: "deleteButton") });
                        if (folderName != "Sent")
                            e.Add(new ContainerElement(null, "Do:") { Buttons = new()
                            {
                                new ButtonJS("Reply", "Reply()"),
                                new ButtonJS("Unread", "Unread()"),
                                new Button("Move", $"{pathPrefix}/move?mailbox={mailbox.Id}&folder={folderName}&message={messageId}")
                            }});
                        Presets.AddError(page);

                        string? c = null;
                        if (hasHtml)
                            c = File.ReadAllText(messagePath + "html");
                        if (c == null && hasText)
                            c = AddHTML(File.ReadAllText(messagePath + "text").HtmlSafe());
                        List<IContent>? textContents = c == null ? null : ReadHTML(c);
                        if (textContents == null || (textContents.Count == 1 && textContents.First() is Paragraph p && (p.Text == "" || p.Text == "<br/>")))
                            e.Add(new ContainerElement("No text attached!", "", "red"));
                        else e.Add(new ContainerElement("Message", textContents));

                        if (message.Attachments.Any())
                        {
                            e.Add(new ContainerElement("Attachments:"));
                            int attachmentId = 0;
                            foreach (var attachment in message.Attachments)
                            {
                                e.Add(new ContainerElement(null, new List<IContent>
                                {
                                    new Paragraph("File: " + attachment.Name ?? "Unknown name"),
                                    new Paragraph("Type: " + attachment.MimeType ?? "Unknown type"),
                                    new Paragraph("Size: " + FileSizeString(new FileInfo($"../Mail/{mailbox.Id}/{messageId}/{attachmentId}").Length))
                                }) { Buttons = new()
                                {
                                    new Button("View", $"/api{pathPrefix}/attachment?mailbox={mailboxId}&message={messageId}&attachment={attachmentId}", newTab: true),
                                    new Button("Download", $"/dl{pathPrefix}/attachment?mailbox={mailboxId}&message={messageId}&attachment={attachmentId}", newTab: true)
                                }
                                });
                                attachmentId++;
                            }
                        }
                        List<IContent> log = new();
                        foreach (var l in message.Log)
                            log.Add(new Paragraph(l));
                        e.Add(new ContainerElement("Log", log));

                        List<string> views = new();
                        if (hasText)
                            views.Add($"<a href=\"{PathWithoutQueries(req, "view", "offset")}&view=text\" target=\"_blank\">Raw text</a>");
                        if (hasHtml)
                        {
                            views.Add($"<a href=\"{PathWithoutQueries(req, "view", "offset")}&view=html\" target=\"_blank\">HTML code</a>");
                            views.Add($"<a href=\"{PathWithoutQueries(req, "view", "offset")}&view=load-html\" target=\"_blank\">Load HTML (dangerous!)</a>");
                        }
                        if (views.Any())
                            e.Add(new ContainerElement("View", new BulletList(views)));
                    }
                } break;
            case "/send":
                {
                    if (InvalidMailbox(req, out var mailbox, e))
                        break;
                    string? to, subject, text;
                    if (mailbox.Messages.TryGetValue(0, out var message))
                    {
                        to = string.Join(';', message.To.Select(x => x.Address));
                        if (to == "") to = null;
                        subject = message.Subject;
                        if (subject == "") subject = null;
                        text = File.Exists($"../Mail/{mailbox.Id}/0/text") ? File.ReadAllText($"../Mail/{mailbox.Id}/0/text") : null;
                        if (text == "") text = null;
                    }
                    else
                    {
                        message = null;
                        to = null; subject = null; text = null;
                    }
                    page.Navigation.Add(new Button("Back", $"{pluginHome}?mailbox={mailbox.Id}", "right"));
                    page.Title = (message == null || message.InReplyToId == null) ? "Send an email" : "Reply";
                    page.HideFooter = true;
                    page.Scripts.Add(new Script(pathPrefix + "/query.js"));
                    page.Scripts.Add(new Script(pathPrefix + "/send.js"));
                    page.Sidebar.Add(new ButtonElement("Mailboxes:", null, pluginHome));
                    foreach (var m in (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : new HashSet<Mailbox>())
                        .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                    {
                        page.Sidebar.Add(new ButtonElement(null, m.Address, $"{pathPrefix}/send?mailbox={m.Id}"));
                    }
                    HighlightSidebar(page, req);
                    page.Styles.Add(new CustomStyle(new List<string>
                    {
                        "#e3 { display: flex; flex-flow: column; }",
                        "#e3 textarea { flex: 1 1 auto; }",
                        "#e3 h1, #e3 h2, #e3 div.buttons { flex: 0 1 auto; }"
                    }));
                    List<IContent> headingContent = new()
                    {
                        new Paragraph($"From: {mailbox.Address}{(mailbox.Name == null ? "" : $" ({mailbox.Name})")}")
                    };
                    if (message != null && message.InReplyToId != null)
                    {
                        headingContent.Add(new Paragraph($"To: {to}"));
                        headingContent.Add(new Paragraph($"Subject: {subject}"));
                    } 
                    e.Add(new LargeContainerElement((message == null || message.InReplyToId == null) ? "Send an email" : "Reply", headingContent, id: "e1") { Button = new ButtonJS("Send", "Send()", "green", id: "send") });
                    e.Add(Presets.ErrorElement);
                    e.Add(new ContainerElement(null, "Draft:", id: "e2") { Buttons = new()
                    {
                        new ButtonJS("Saved!", "Save()", id: "save"),
                        new ButtonJS("Discard", "Discard()", "red", id: "discardButton")
                    }});
                    List<IContent> inputs = new();
                    if (message == null || message.InReplyToId == null)
                    {
                        inputs.Add(new TextBox("Recipient(s)...", to, "to", TextBoxRole.Email, autofocus: true, onInput: "MessageChanged()"));
                        inputs.Add(new TextBox("Subject...", subject, "subject", onInput: "MessageChanged()"));
                    }
                    inputs.Add(new TextArea("Message...", text, "text", onInput: "MessageChanged(); Resize()"));
                    e.Add(new LargeContainerElement(null, inputs, id: "e3"));
                    int attachmentCount = message == null ? 0 : message.Attachments.Count;
                    e.Add(new ButtonElementJS(null, $"Attachments ({attachmentCount})", "GoToAttachments()", id: "e4"));
                }
                break;
            case "/send/attachments":
                {
                    if (InvalidMailbox(req, out var mailbox, e))
                        break;
                    page.Navigation.Add(new Button("Back", $"{pluginHome}?mailbox={mailbox.Id}", "right"));
                    page.Title = "Send an email";
                    page.Scripts.Add(new Script(pathPrefix + "/query.js"));
                    page.Scripts.Add(new Script(pathPrefix + "/send-attachments.js"));
                    page.Sidebar.Add(new ButtonElement("Mailboxes:", null, pluginHome));
                    foreach (var m in (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : new HashSet<Mailbox>())
                        .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                    {
                        page.Sidebar.Add(new ButtonElement(null, m.Address, $"{pathPrefix}/send/attachments?mailbox={m.Id}"));
                    }
                    HighlightSidebar(page, req);
                    e.Add(new LargeContainerElement("Send an email", new List<IContent>
                    {
                        new Paragraph($"From: {mailbox.Address}{(mailbox.Name == null ? "" : $" ({mailbox.Name})")}"),
                    }, id: "e1")
                    { Button = new Button("Back", $"{pathPrefix}/send?mailbox={mailbox.Id}") });
                    e.Add(new ContainerElement("Add attachment:", new FileSelector("update-file")) { Button = new ButtonJS("Add", "Upload()", "green", id: "uploadButton") });
                    page.AddError();
                    int attachmentCount = Directory.Exists($"../Mail/{mailbox.Id}/0") ? Directory.GetFiles($"../Mail/{mailbox.Id}/0").Select(x => x.After('/').After('\\')).Count(x => x != "text") : 0;
                    if (mailbox.Messages.TryGetValue(0, out var message))
                    {
                        int counter = 0;
                        foreach (var attachment in message.Attachments)
                        {
                            e.Add(new ContainerElement(null, attachment.Name ?? "No file name", "overflow") { Button = new ButtonJS("Remove", $"Delete('{counter}')", "red") });
                            counter++;
                        }
                    }
                }
                break;
            case "/move":
                {
                    if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out var message, out var messageId, out _, out var folderName, e))
                        break;
                    if (folderName == "Sent")
                    {
                        req.Status = 400;
                        break;
                    }
                    page.Navigation.Add(new Button("Back", $"{pluginHome}?mailbox={mailbox.Id}&folder={folderName}&message={messageId}", "right"));
                    page.Title = "Move";
                    page.Scripts.Add(new Script(pathPrefix + "/query.js"));
                    page.Scripts.Add(new Script(pathPrefix + "/move.js"));
                    e.Add(new LargeContainerElement("Moving", message.Subject) { Button = new Button("Cancel", $"{pluginHome}?mailbox={mailbox.Id}&folder={folderName}&message={messageId}", "red") });
                    page.AddError();
                    foreach (var f in SortFolders(mailbox.Folders.Keys))
                        if (f == "Sent")
                            continue;
                        else if (f == folderName)
                            e.Add(new ContainerElement(f, "", "green"));
                        else
                            e.Add(new ButtonElementJS(f, null, $"Move('{HttpUtility.UrlEncode(f)}')"));
                }
                break;
            case "/settings":
                {
                    if (InvalidMailbox(req, out var mailbox, e))
                        break;
                    page.Navigation.Add(new Button("Back", $"{pluginHome}?mailbox={mailbox.Id}", "right"));
                    page.Title = "Mail settings";
                    page.Scripts.Add(new Script(pathPrefix + "/query.js"));
                    page.Scripts.Add(new Script(pathPrefix + "/settings.js"));
                    page.Sidebar.Add(new ButtonElement("Mailboxes:", null, pluginHome));
                    foreach (var m in (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : new HashSet<Mailbox>())
                        .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                        page.Sidebar.Add(new ButtonElement(null, m.Address, $"{pathPrefix}/settings?mailbox={m.Id}"));
                    HighlightSidebar(page, req);
                    e.Add(new LargeContainerElement("Mail settings", mailbox.Address));
                    page.AddError();
                    e.Add(new ButtonElement("Folders", null, $"{pathPrefix}/settings/folders?mailbox={mailbox.Id}"));
                    e.Add(new ContainerElement("Name", new TextBox("Enter a name...", mailbox.Name, "name-input", onEnter: "SaveName()", onInput: "NameChanged()")) { Button = new ButtonJS("Saved!", "SaveName()", id: "save-name") });
                }
                break;
            case "/settings/folders":
                {
                    if (InvalidMailbox(req, out var mailbox, e))
                        break;
                    page.Navigation.Add(new Button("Back", $"{pathPrefix}/settings?mailbox={mailbox.Id}", "right"));
                    page.Title = "Mail folders";
                    page.Scripts.Add(new Script(pathPrefix + "/query.js"));
                    page.Scripts.Add(new Script(pathPrefix + "/settings-folders.js"));
                    page.Sidebar.Add(new ButtonElement("Mailboxes:", null, pluginHome));
                    foreach (var m in (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : new HashSet<Mailbox>())
                        .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                        page.Sidebar.Add(new ButtonElement(null, m.Address, $"{pathPrefix}/settings/folders?mailbox={m.Id}"));
                    HighlightSidebar(page, req);
                    e.Add(new LargeContainerElement("Mail folders", new List<IContent> { new Paragraph(mailbox.Address), new Paragraph("Warning: Deleting a folder will delete all of the messages within it!") }));
                    e.Add(new ContainerElement("New folder", new TextBox("Enter a name...", null, "name", onEnter: "Create()", autofocus: true)) { Button = new ButtonJS("Create", "Create()", "green")});
                    page.AddError();
                    foreach (var f in SortFolders(mailbox.Folders.Keys))
                    {
                        if (new[] { "Inbox", "Sent", "Spam", "Trash" }.Contains(f))
                            e.Add(new ContainerElement(null, f));
                        else e.Add(new ContainerElement(null, f) { Button = new ButtonJS("Delete", $"Delete('{HttpUtility.UrlEncode(f)}', '{f.ToId()}')", "red", id: f.ToId()) });
                    }
                }
                break;
            case "/manage":
                if (!req.IsAdmin())
                {
                    req.Status = 403;
                    break;
                }
                else
                {
                    //create and delete mailboxes, add and remove access to them (update the cache dictionary in both cases!!!)
                    if (req.Query.TryGetValue("mailbox", out string? mailboxId))
                    {
                        if (!Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                        {
                            //mailbox doesn't exist
                            e.Add(new LargeContainerElement("Error", "This mailbox doesn't exist!", "red"));
                            break;
                        }
                        //manage mailbox
                        page.Navigation.Add(new Button("Back", $"{pathPrefix}/manage", "right"));
                        page.Title = "Manage " + mailbox.Address;
                        page.Scripts.Add(new Script(pathPrefix + "/query.js"));
                        page.Scripts.Add(new Script(pathPrefix + "/manage-mailbox.js"));
                        page.Sidebar.Add(new ContainerElement("Mailboxes:", ""));
                        foreach (var m in Mailboxes.Select(x => x.Value).OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                        {
                            page.Sidebar.Add(new ButtonElement(null, m.Address, $"{pathPrefix}/manage?mailbox={m.Id}"));
                        }
                        HighlightSidebar(page, req);
                        e.Add(new LargeContainerElement("Manage " + mailbox.Address));
                        e.Add(new ButtonElementJS("Delete mailbox", null, "Delete()", id: "deleteButton"));
                        e.Add(new ContainerElement("Add access:", new TextBox("Enter a username...", null, "username", onEnter: "Add()")) { Button = new ButtonJS("Add", "Add()", "green") });
                        Presets.AddError(page);
                        if (mailbox.AllowedUserIds.Any(x => x.Value.Any()))
                        {
                            foreach (var userTableKV in mailbox.AllowedUserIds)
                            {
                                UserTable userTable = UserTable.Import(userTableKV.Key);
                                foreach (var userId in userTableKV.Value)
                                    if (userTable.TryGetValue(userId, out User? u))
                                        e.Add(new ContainerElement(u.Username, u.Id) { Button = new ButtonJS("Remove", $"Remove('{userTable.Name}:{userId}')", "red") });
                            }
                        }
                        else
                        {
                            e.Add(new ContainerElement("No allowed accounts!", "", "red"));
                        }
                    }
                    else
                    {
                        //list mailboxes to manage them, and offer to create a new one
                        page.Navigation.Add(new Button("Back", pluginHome, "right"));
                        page.Title = "Manage mailboxes";
                        page.Scripts.Add(new Script(pathPrefix + "/manage.js"));
                        e.Add(new LargeContainerElement("Manage mailboxes", ""));
                        var mailboxes = Mailboxes.Select(x => x.Value).OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@'));
                        e.Add(new ContainerElement("Create mailbox:", new TextBox("Enter an address...", null, "address", onEnter: "Create()")) { Button = new ButtonJS("Create", "Create()", "green") });
                        Presets.AddError(page);
                        if (mailboxes.Any())
                        {
                            foreach (Mailbox m in mailboxes)
                            {
                                e.Add(new ButtonElement(m.Address, m.Name ?? "", $"{pathPrefix}/manage?mailbox={m.Id}"));
                            }
                        }
                        else
                        {
                            e.Add(new ContainerElement("No mailboxes!", "", "red"));
                        }
                    }
                }
                break;
            default:
                req.Status = 404;
                break;
        }
        return Task.CompletedTask;
    }

    private static string PathWithoutQueries(AppRequest req, params string[] queries)
    {
        string query = string.Join('&', req.Context.Request.Query.Where(x => !queries.Contains(x.Key)).Select(x => $"{x.Key}={(x.Key == "folder" ? HttpUtility.UrlEncode(x.Value) : x.Value)}"));
        if (query == "")
            return req.Path;
        else return $"{req.Path}?{query}";
    }

    private static void HighlightSidebar(Page page, AppRequest req, params string[] ignoredQueries)
    {
        string url = PathWithoutQueries(req, ignoredQueries);
        foreach (IPageElement element in page.Sidebar)
            if (element is ButtonElement button && button.Link == url)
                button.Class = "green";
    }
}