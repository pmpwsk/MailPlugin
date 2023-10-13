using Microsoft.AspNetCore.Mvc.Filters;
using MimeKit;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Threading;
using System.Web;
using uwap.WebFramework.Accounts;
using uwap.WebFramework.Elements;
using static MailKit.Net.Imap.ImapMailboxFilter;
using static QRCoder.PayloadGenerator;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
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
        switch (path)
        {
            case "":
                {
                    page.Scripts.Add(new CustomScript($"document.cookie = \"TimeOffset=\" + new Date().getTimezoneOffset() + \"; domain={req.Domain}; path=/\";"));
                    if (!req.Query.TryGetValue("mailbox", out string? mailboxId))
                    {/////
                        //list mailboxes that the user has access to or an error if none were found, redirect to mailbox if only one is present and user isn't admin
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
                                foreach (Mailbox m in mailboxes)
                                {
                                    bool unread = GetLastReversed(m.Folders["Inbox"], MessagePreloadCount, 0).Any(x => m.Messages.TryGetValue(x, out var message) && message.Unread);
                                    e.Add(new ButtonElement((unread ? "(!) " : "") + m.Address, m.Name ?? "", $"{pluginHome}?mailbox={m.Id}", unread ? "red" : null));
                                }
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
                        e.Add(new LargeContainerElement("Error", "This mailbox doesn't exist!", "red"));
                        break;
                    }
                    if ((!mailbox.AllowedUserIds.TryGetValue(req.UserTable.Name, out var allowedUserIds)) || !allowedUserIds.Contains(req.User.Id))
                    {
                        //user doesn't have access to this mailbox
                        e.Add(new LargeContainerElement("Error", "You don't have access to this mailbox!", "red"));
                        break;
                    }

                    if (!req.Query.TryGetValue("folder", out var folderName))
                    {/////
                        //list folders in the mailbox (inbox, sent, recycle bin, spam are pinned)
                        page.Title = $"Mail ({mailbox.Address})";
                        page.Sidebar.Add(new ButtonElement("Mailboxes:", null, $"{pluginHome}"));
                        foreach (Mailbox m in (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : new HashSet<Mailbox>())
                            .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                        {
                            bool unread = GetLastReversed(m.Folders["Inbox"], MessagePreloadCount, 0).Any(x => m.Messages.TryGetValue(x, out var message) && message.Unread);
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
                            e.Add(new ButtonElement((unread ? "(!) " : "") + folderItem.Key, null, $"{pluginHome}?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderItem.Key)}", unread ? "red" : null));
                        }
                        break;
                    }
                    if (!mailbox.Folders.TryGetValue(folderName, out var folder))
                    {
                        //folder doesn't exist
                        e.Add(new LargeContainerElement("Error", "This folder doesn't exist!", "red"));
                        break;
                    }

                    if (!req.Query.TryGetValue("message", out var messageIdString))
                    {/////
                        //list n messages (with offset) in the folder
                        page.Title = $"{folderName} ({mailbox.Address})";
                        page.Sidebar.Add(new ButtonElement("Folders:", null, $"{pluginHome}?mailbox={mailboxId}"));
                        foreach (var folderItem in SortFolders(mailbox.Folders))
                        {
                            bool unread = GetLastReversed(folderItem.Value, MessagePreloadCount, 0).Any(x => mailbox.Messages.TryGetValue(x, out var message) && message.Unread);
                            page.Sidebar.Add(new ButtonElement(null, (unread ? "(!) " : "") + folderItem.Key, $"{pluginHome}?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderItem.Key)}", unread ? "red" : null));
                        }
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
                                    e.Add(new ButtonElement(m.Subject, m.From.FullString, $"{pluginHome}?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderName)}&message={mId}{offsetQuery}", m.Unread ? "red" : null));
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
                        string view = req.Query.TryGet("view") ?? "text";
                        switch (view)
                        {
                            case "text":
                            case "converted":
                            case "html":
                                break;
                            case "load-html":
                                req.Page = new RawHtmlPage($"../Mail/{mailboxId}/{messageId}/html");
                                return Task.CompletedTask;
                            default:
                                req.Status = 400;
                                return Task.CompletedTask;
                        }
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
                        List<IContent> contents = new();
                        if (message.InReplyToId != null)
                            contents.Add(new Paragraph($"This is a reply to another email (<a href=\"javascript:\" onclick=\"FindOriginal('{HttpUtility.UrlEncode(message.InReplyToId)}')\">find</a>)."));
                        contents.Add(new Paragraph(DateTimeString(AdjustDateTime(req, message.TimestampUtc))));
                        contents.Add(new Paragraph("From: " + message.From.FullString));
                        foreach (var to in message.To)
                            contents.Add(new Paragraph("To: " + to.FullString));
                        foreach (var cc in message.Cc)
                            contents.Add(new Paragraph("CC: " + cc.FullString));
                        foreach (var bcc in message.Bcc)
                            contents.Add(new Paragraph("BCC: " + bcc.FullString));
                        e.Add(new LargeContainerElement($"{message.Subject} ({mailbox.Address})", contents) { Button = new ButtonJS("Delete", "Delete()", "red", id: "deleteButton") });
                        if (folderName != "Sent")
                            e.Add(new ContainerElement(null, "Actions:") { Buttons = new()
                            {
                                new ButtonJS("Reply", "Reply()"),
                                new ButtonJS("Unread", "Unread()"),
                                new Button("Move", $"{pathPrefix}/move?mailbox={mailbox.Id}&folder={folderName}&message={messageId}")
                            }});
                        Presets.AddError(page);
                        e.Add(new ContainerElement(null, "View:") { Buttons = new()
                        {
                            new Button("Text", $"{PathWithoutQueries(req, "view", "offset")}&view=text{offsetQuery}", view == "text" ? "green" : null),
                            new Button("Converted", $"{PathWithoutQueries(req, "view", "offset")}&view=converted{offsetQuery}", view == "converted" ? "green" : null),
                            new Button("HTML", $"{PathWithoutQueries(req, "view", "offset")}&view=html{offsetQuery}", view == "html" ? "green" : null)
                        }
                        });
                        string messagePath = $"../Mail/{mailboxId}/{messageId}/";
                        switch (view)
                        {
                            case "text":
                                if (File.Exists(messagePath + "text"))
                                    e.Add(new ContainerElement("Text", File.ReadAllText(messagePath + "text").HtmlSafe().Replace("\n", "<br/>")));
                                else e.Add(new ContainerElement("No text attached!", "", "red"));
                                break;
                            case "converted":
                                if (File.Exists(messagePath + "html"))
                                    e.Add(new ContainerElement("Converted HTML", ConvertHtml(File.ReadAllText(messagePath + "html")).ToList()));
                                else e.Add(new ContainerElement("No HTML attached!", "", "red"));
                                break;
                            case "html":
                                if (File.Exists(messagePath + "html"))
                                    e.Add(new ContainerElement("HTML", File.ReadAllText(messagePath + "html").HtmlSafe().Replace("\n", "<br/>")));
                                else e.Add(new ContainerElement("No HTML attached!", "", "red"));
                                break;
                        }
                        if (message.Attachments.Any())
                        {
                            e.Add(new ContainerElement("Attachments:"));
                            int attachmentId = 0;
                            foreach (var attachment in message.Attachments)
                            {
                                e.Add(new ContainerElement(null, new List<IContent> { new Paragraph(attachment.Name ?? "Unknown name"), new Paragraph(attachment.MimeType ?? "Unknown type") }) { Buttons = new()
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
                        if (File.Exists(messagePath + "html"))
                            e.Add(new ButtonElement(null, "Load HTML (dangerous!)", $"{PathWithoutQueries(req, "offset", "view")}&view=load-html", newTab: true));
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
                    page.Title = "Mail settings";
                    page.Scripts.Add(new Script(pathPrefix + "/query.js"));
                    page.Scripts.Add(new Script(pathPrefix + "/settings.js"));
                    page.Sidebar.Add(new ButtonElement("Mailboxes:", null, pluginHome));
                    foreach (var m in (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : new HashSet<Mailbox>())
                        .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                        page.Sidebar.Add(new ButtonElement(null, m.Address, $"{pathPrefix}/settings?mailbox={m.Id}"));
                    HighlightSidebar(page, req);
                    e.Add(new LargeContainerElement("Mail settings", mailbox.Address));
                    e.Add(new ButtonElement("Folders", null, $"{pathPrefix}/settings/folders?mailbox={mailbox.Id}"));
                    e.Add(new ContainerElement("Name", new TextBox("Enter a name...", mailbox.Name, "name-input", onEnter: "SaveName()", onInput: "NameChanged()")) { Button = new ButtonJS("Saved!", "SaveName()", id: "save-name") });
                }
                break;
            case "/settings/folders":
                {
                    if (InvalidMailbox(req, out var mailbox, e))
                        break;
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

    private static IEnumerable<IContent> ConvertHtml(string html)
    {
        yield return new Paragraph("This view hasn't been implemented yet!");
    }
}