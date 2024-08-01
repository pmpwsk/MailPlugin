using System.Web;
using uwap.WebFramework.Elements;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public async Task HandleOther(Request req)
    {
        switch (req.Path)
        {
            // MAIN
            case "/":
            { CreatePage(req, "Mail", out var page, out var e);
                page.Scripts.Add(new CustomScript($"var cookie = \"TimeOffset=\" + new Date().getTimezoneOffset();\nif (!document.cookie.split(\";\").some(x => x.trim() === cookie)) {{\n\tvar d8 = new Date();\n\td8.setTime(d8.getTime() + 7776000000);\n\tdocument.cookie = cookie + \"; domain={req.Domain}; path=/; SameSite=Strict; expires=\" + d8.toUTCString();\n}}"));
                if (!req.Query.TryGetValue("mailbox", out string? mailboxId))
                {/////
                    //list mailboxes that the user has access to or an error if none were found, redirect to mailbox if only one is present and user isn't admin
                    page.Navigation.Add(new Button("Back", "/", "right"));
                    page.Title = "Mailboxes";
                    bool isAdmin = req.IsAdmin;
                    var mailboxes = (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : [])
                        .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@'));

                    if (isAdmin || mailboxes.Count() != 1)
                    {
                        e.Add(new LargeContainerElement("Mailboxes", ""));
                        if (isAdmin)
                        {
                            e.Add(new ButtonElement("Manage mailboxes", "", "manage"));
                        }
                        if (mailboxes.Any())
                        {
                            page.Scripts.Add(IncomingScript(req, mailboxes.Max(LastInboxMessageId)));
                            bool anyUnread = false;
                            foreach (Mailbox m in mailboxes)
                            {
                                bool unread = GetLastReversed(m.Folders["Inbox"], MessagePreloadCount, 0).Any(x => m.Messages.TryGetValue(x, out var message) && message.Unread);
                                if (unread) anyUnread = true;
                                e.Add(new ButtonElement((unread ? "(!) " : "") + m.Address, m.Name ?? "", $".?mailbox={m.Id}", unread ? "red" : null));
                            }
                            if (anyUnread)
                                page.Favicon = $"{req.PluginPathPrefix}/icon-red.ico";
                        }
                        else
                        {
                            e.Add(new ContainerElement("No mailboxes!", "If you'd like to get your own mailbox, please contact the support.", "red"));
                            Presets.AddSupportButton(page, req);
                        }
                    }
                    else req.Redirect($".?mailbox={mailboxes.First().Id}");
                    break;
                }
                if (!Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                {
                    //mailbox doesn't exist
                    page.Navigation.Add(new Button("Back", ".", "right"));
                    e.Add(new LargeContainerElement("Error", "This mailbox doesn't exist!", "red"));
                    break;
                }
                if ((!mailbox.AllowedUserIds.TryGetValue(req.UserTable.Name, out var allowedUserIds)) || !allowedUserIds.Contains(req.User.Id))
                {
                    //user doesn't have access to this mailbox
                    page.Navigation.Add(new Button("Back", ".", "right"));
                    e.Add(new LargeContainerElement("Error", "You don't have access to this mailbox!", "red"));
                    break;
                }

                if (!req.Query.TryGetValue("folder", out var folderName))
                {/////
                    //list folders in the mailbox (inbox, sent, recycle bin, spam are pinned)
                    var mailboxes = (Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : [])
                        .OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')).ToList();
                    page.Navigation.Add(new Button("Back", mailboxes.Count == 1 && !req.IsAdmin ? "/" : ".", "right"));
                    page.Scripts.Add(IncomingScript(req, LastInboxMessageId(mailbox)));
                    page.Title = $"Mail ({mailbox.Address})";
                    page.Sidebar.Add(new ButtonElement("Mailboxes:", null, "."));
                    bool anyUnread = false;
                    foreach (Mailbox m in mailboxes)
                    {
                        bool unread = GetLastReversed(m.Folders["Inbox"], MessagePreloadCount, 0).Any(x => m.Messages.TryGetValue(x, out var message) && message.Unread);
                        if (unread) anyUnread = true;
                        page.Sidebar.Add(new ButtonElement(null, (unread ? "(!) " : "") + m.Address, $".?mailbox={m.Id}", unread ? "red" : null));
                    }
                    HighlightSidebar(".", page, req);
                    e.Add(new LargeContainerElement($"Mail ({mailbox.Address})", "", "overflow"));
                    e.Add(new ContainerElement(null, "Actions:") { Buttons =
                    [
                        new Button("Settings", $"settings?mailbox={mailboxId}"),
                        new Button("Send", $"send?mailbox={mailboxId}", "green"),
                    ]});
                    foreach (var folderItem in SortFolders(mailbox.Folders))
                    {
                        bool unread = GetLastReversed(folderItem.Value, MessagePreloadCount, 0).Any(x => mailbox.Messages.TryGetValue(x, out var message) && message.Unread);
                        if (unread) anyUnread = true;
                        e.Add(new ButtonElement((unread ? "(!) " : "") + folderItem.Key, CountString(folderItem.Value.Count, "message"), $".?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderItem.Key)}", unread ? "red" : null));
                    }
                    if (anyUnread)
                        page.Favicon = $"{req.PluginPathPrefix}/icon-red.ico";
                    break;
                }
                if (!mailbox.Folders.TryGetValue(folderName, out var folder))
                {
                    //folder doesn't exist
                    page.Navigation.Add(new Button("Back", ".", "right"));
                    e.Add(new LargeContainerElement("Error", "This folder doesn't exist!", "red"));
                    break;
                }

                if (!req.Query.TryGetValue("message", out var messageIdString))
                {/////
                    //list n messages (with offset) in the folder
                    page.Navigation.Add(new Button("Back", $".?mailbox={mailboxId}", "right"));
                    page.Title = $"{folderName} ({mailbox.Address})";
                    page.Scripts.Add(IncomingScript(req, LastInboxMessageId(mailbox)));
                    page.Sidebar.Add(new ButtonElement("Folders:", null, $".?mailbox={mailboxId}"));
                    bool anyUnread = false;
                    foreach (var folderItem in SortFolders(mailbox.Folders))
                    {
                        bool unread = GetLastReversed(folderItem.Value, MessagePreloadCount, 0).Any(x => mailbox.Messages.TryGetValue(x, out var message) && message.Unread);
                        if (unread) anyUnread = true;
                        page.Sidebar.Add(new ButtonElement(null, (unread ? "(!) " : "") + folderItem.Key, $".?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderItem.Key)}", unread ? "red" : null));
                    }
                    if (anyUnread)
                        page.Favicon = $"{req.PluginPathPrefix}/icon-red.ico";
                    HighlightSidebar(".", page, req);
                    e.Add(new LargeContainerElement($"{folderName} ({mailbox.Address})", ""));
                    if (folder.Count != 0)
                    {
                        int offset = 0;
                        if (req.Query.TryGetValue("offset", out var offsetString) && int.TryParse(offsetString, out int offset2) && offset2 >= 0)
                            offset = offset2;
                        if (offset > 0)
                            e.Add(new ButtonElement(null, "Newer messages", $"{PathWithoutQueries(".", req, "offset")}&offset={Math.Max(offset - MessagePreloadCount, 0)}"));
                        string offsetQuery = offset == 0 ? "" : $"&offset={offset}";
                        foreach (var mId in GetLastReversed(folder, MessagePreloadCount, offset))
                            if (mailbox.Messages.TryGetValue(mId, out var m))
                                e.Add(new ButtonElement(m.Subject, (folderName == "Sent" ? "To: " + string.Join(", ", m.To.Select(x => x.ContactString(mailbox))) : m.From.ContactString(mailbox)) + "<br/>" + DateTimeString(AdjustDateTime(req, m.TimestampUtc)), $".?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderName)}&message={mId}{offsetQuery}", m.Unread ? "red" : null));
                        if (offset + MessagePreloadCount < folder.Count)
                            e.Add(new ButtonElement(null, "Older messages", $"{PathWithoutQueries(".", req, "offset")}&offset={offset + MessagePreloadCount}"));
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
                    page.Navigation.Add(new Button("Back", ".", "right"));
                    e.Add(new LargeContainerElement("Error", "This message doesn't exist!", "red"));
                    break;
                }
                if (!folder.Contains(messageId))
                {
                    //message isn't part of the folder, try to find the new folder and redirect
                    req.Redirect($".?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(mailbox.Folders.First(x => x.Value.Contains(messageId)).Key)}&message={messageIdString}");
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
                    string messagePath = $"../MailPlugin.Mailboxes/{mailboxId}/{messageId}/";
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
                                req.Page = new RawHtmlFilePage($"../MailPlugin.Mailboxes/{mailboxId}/{messageId}/html");
                                break;
                            default:
                                throw new BadRequestSignal();
                        }
                        break;
                    }

                    bool hasText = File.Exists(messagePath + "text");
                    bool hasHtml = File.Exists(messagePath + "html");

                    page.Navigation.Add(new Button("Back", PathWithoutQueries(".", req, "message", "view", "offset"), "right"));
                    page.Title = $"{message.Subject} ({mailbox.Address})";
                    page.Scripts.Add(Presets.SendRequestScript);
                    page.Scripts.Add(IncomingScript(req, LastInboxMessageId(mailbox)));
                    page.Scripts.Add(new Script("query.js"));
                    page.Scripts.Add(new Script("message.js"));
                    page.Sidebar.Add(new ButtonElement("Messages:", null, PathWithoutQueries(".", req, "message", "view", "offset")));
                    int offset = 0;
                    if (req.Query.TryGetValue("offset", out var offsetString) && int.TryParse(offsetString, out int offset2) && offset2 >= 0)
                        offset = offset2;
                    if (offset > 0)
                        page.Sidebar.Add(new ButtonElement(null, "Newer messages", $"{PathWithoutQueries(".", req, "offset")}&offset={Math.Max(offset - MessagePreloadCount, 0)}"));
                    string offsetQuery = offset == 0 ? "" : $"&offset={offset}";
                    bool anyUnread = false;
                    foreach (var mId in GetLastReversed(folder, MessagePreloadCount, offset))
                        if (mailbox.Messages.TryGetValue(mId, out var m))
                        {
                            if (m.Unread)
                                anyUnread = true;
                            page.Sidebar.Add(new ButtonElement(null, $"{(folderName == "Sent" ? "To: " + string.Join(", ", m.To.Select(x => x.ContactString(mailbox))) : m.From.ContactString(mailbox))}:<br/>{m.Subject}", $".?mailbox={mailboxId}&folder={HttpUtility.UrlEncode(folderName)}&message={mId}{offsetQuery}", m.Unread ? "red" : null));
                        }
                    if (anyUnread)
                        page.Favicon = $"{req.PluginPathPrefix}/icon-red.ico";
                    if (offset + MessagePreloadCount < folder.Count)
                        page.Sidebar.Add(new ButtonElement(null, "Older messages", $"{PathWithoutQueries(".", req, "offset")}&offset={offset + MessagePreloadCount}"));
                    HighlightSidebar(".", page, req, "view");
                    List<IContent> headingContents = [];
                    if (message.InReplyToId != null)
                        headingContents.Add(new Paragraph($"This is a reply to another email (<a href=\"javascript:\" id=\"find\" onclick=\"FindOriginal('{HttpUtility.UrlEncode(message.InReplyToId)}')\">find</a>)."));
                    headingContents.Add(new Paragraph(DateTimeString(AdjustDateTime(req, message.TimestampUtc))));
                    headingContents.Add(new Paragraph("From: " + message.From.ContactString(mailbox)));
                    foreach (var to in message.To)
                        headingContents.Add(new Paragraph("To: " + to.ContactString(mailbox)));
                    foreach (var cc in message.Cc)
                        headingContents.Add(new Paragraph("CC: " + cc.ContactString(mailbox)));
                    foreach (var bcc in message.Bcc)
                        headingContents.Add(new Paragraph("BCC: " + bcc.ContactString(mailbox)));
                    e.Add(new LargeContainerElement($"{message.Subject}", headingContents) { Button = new ButtonJS("Delete", "Delete()", "red", id: "deleteButton") });
                    if (folderName != "Sent")
                        e.Add(new ContainerElement(null, "") { Buttons =
                        [
                            new ButtonJS("Reply", "Reply()"),
                            new ButtonJS("Unread", "Unread()"),
                            new Button("Forward", $"forward?mailbox={mailbox.Id}&folder={folderName}&message={messageId}"),
                            new Button("Move", $"move?mailbox={mailbox.Id}&folder={folderName}&message={messageId}")
                        ]});
                    Presets.AddError(page);

                    string? c = null;
                    if (hasHtml)
                        c = File.ReadAllText(messagePath + "html");
                    if (c == null && hasText)
                        c = AddHTML(File.ReadAllText(messagePath + "text").HtmlSafe());
                    List<IContent>? textContents = c == null ? null : ReadHTML(c, mailbox.ShowExternalImageLinks);
                    if (textContents == null || (textContents.Count == 1 && textContents.First() is Paragraph p && (p.Text == "" || p.Text == "<br/>")))
                        e.Add(new ContainerElement("No text attached!", "", "red"));
                    else e.Add(new ContainerElement("Message", textContents));

                    if (message.Attachments.Count != 0)
                    {
                        e.Add(new ContainerElement("Attachments:"));
                        int attachmentId = 0;
                        foreach (var attachment in message.Attachments)
                        {
                            e.Add(new ContainerElement(null,
                            [
                                new Paragraph("File: " + attachment.Name ?? "Unknown name"),
                                new Paragraph("Type: " + attachment.MimeType ?? "Unknown type"),
                                new Paragraph("Size: " + FileSizeString(new FileInfo($"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}/{attachmentId}").Length))
                            ]) { Buttons =
                                [
                                    new Button("View", $"attachment?mailbox={mailboxId}&message={messageId}&attachment={attachmentId}&download=false", newTab: true),
                                    new Button("Download", $"attachment?mailbox={mailboxId}&message={messageId}&attachment={attachmentId}&download=true", newTab: true)
                                ]
                            });
                            attachmentId++;
                        }
                    }
                    List<IContent> log = [];
                    foreach (var l in message.Log)
                        log.Add(new Paragraph(l));
                    e.Add(new ContainerElement("Log", log));

                    List<string> views = [];
                    if (hasText)
                        views.Add($"<a href=\"{PathWithoutQueries(".", req, "view", "offset")}&view=text\" target=\"_blank\">Raw text</a>");
                    if (hasHtml)
                    {
                        views.Add($"<a href=\"{PathWithoutQueries(".", req, "view", "offset")}&view=html\" target=\"_blank\">HTML code</a>");
                        views.Add($"<a href=\"{PathWithoutQueries(".", req, "view", "offset")}&view=load-html\" target=\"_blank\">Load HTML (dangerous!)</a>");
                    }
                    if (views.Count != 0)
                        e.Add(new ContainerElement("View", new BulletList(views)));
                }
            } break;

            case "/attachment":
            { GET(req);
                if (InvalidMailboxOrMessage(req, out var mailbox, out var message, out var messageId, true))
                    break;
                if ((!req.Query.TryGetValue("attachment", out string? attachmentIdString)) || (!int.TryParse(attachmentIdString, out var attachmentId)) || !req.Query.TryGetValue("download", out bool download))
                    throw new BadRequestSignal();
                if (attachmentId < 0 || attachmentId >= message.Attachments.Count)
                    throw new NotFoundSignal();
                string filePath = $"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}/{attachmentId}";
                if (!File.Exists(filePath))
                    throw new ServerErrorSignal();
                MailAttachment attachment = message.Attachments[attachmentId];
                req.Context.Response.ContentType = attachment.MimeType;
                if (download)
                    await req.WriteFileAsDownload(filePath, attachment.Name);
                else await req.WriteFile(filePath);
            } break;
                
            case "/delete-message":
            { POST(req);
                if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out var message, out var messageId, out var folder, out var folderName))
                    break;
                mailbox.Lock();
                if (folderName == "Trash")
                {
                    string messagePath = $"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}";
                    if (Directory.Exists(messagePath))
                        Directory.Delete(messagePath, true);
                    mailbox.Messages.Remove(messageId);
                    folder.Remove(messageId);
                }
                else
                {
                    message.Deleted = DateTime.UtcNow;
                    folder.Remove(messageId);
                    mailbox.Folders["Trash"].Add(messageId);
                }
                mailbox.UnlockSave();
                await req.Write("ok");
            } break;

            case "/unread":
            { POST(req);
                if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out var message, out _, out _, out var folderName))
                    break;
                if (folderName == "Sent")
                    throw new BadRequestSignal();
                mailbox.Lock();
                message.Unread = true;
                mailbox.UnlockSave();
            } break;

            case "/reply":
            { POST(req);
                if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out var message, out var messageId, out _, out var folderName))
                    break;
                if (folderName == "Sent")
                    throw new BadRequestSignal();
                mailbox.Lock();
                string? text = null;
                string messagePath = $"../MailPlugin.Mailboxes/{mailbox.Id}/{messageId}/";
                if (File.Exists(messagePath + "html"))
                    text = File.ReadAllText(messagePath + "html");
                if (text == null && File.Exists(messagePath + "text"))
                    text = AddHTML(File.ReadAllText(messagePath + "text").HtmlSafe());
                if (text != null)
                    text = $"\n\n\n# Original message:\n# From: {message.From.FullString}\n# Time: {DateTimeString(message.TimestampUtc)} UTC\n\n\n{QuoteHTML(Before(text, "# Original message:").TrimEnd())}";
                else text = "";

                if (mailbox.Footer != null)
                    text = "\n\n" + mailbox.Footer + text;

                string subject = message.Subject.Trim();
                while (subject.SplitAtFirst(':', out var subjectPrefix, out var realSubject) && subjectPrefix.All(char.IsLetter) && (subjectPrefix.Length == 2 || subjectPrefix.Length == 3) && realSubject.TrimStart() != "")
                    subject = realSubject.TrimStart();
                string toAddress = (message.ReplyTo ?? message.From).Address;
                mailbox.Messages[0] = new(new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address), [new MailAddress(toAddress, mailbox.Contacts.TryGetValue(toAddress, out var contact) ? contact.Name : toAddress)], "Re: " + subject, message.MessageId);
                Directory.CreateDirectory($"../MailPlugin.Mailboxes/{mailbox.Id}/0");
                File.WriteAllText($"../MailPlugin.Mailboxes/{mailbox.Id}/0/text", text ?? "");
                mailbox.UnlockSave();
            } break;

            case "/find":
            { POST(req);
                if (InvalidMailbox(req, out var mailbox))
                    break;
                if (!req.Query.TryGetValue("id", out var id))
                    throw new BadRequestSignal();
                var messageKV = mailbox.Messages.LastOrDefault(x => x.Value.MessageId.Split('\n').Contains(id));
                if (messageKV.Equals(default(KeyValuePair<ulong,MailMessage>)))
                {
                    await req.Write("no");
                    break;
                }
                ulong messageId = messageKV.Key;
                await req.Write($"mailbox={mailbox.Id}&folder={HttpUtility.UrlEncode(mailbox.Folders.First(x => x.Value.Contains(messageId)).Key)}&message={messageId}");
            } break;




            // INCOMING MESSAGE EVENT
            case "/incoming-event":
            { GET(req);
                req.KeepEventAliveCancelled += RemoveIncomingListener;
                var mailboxes = Mailboxes.UserAllowedMailboxes.TryGetValue(req.UserTable.Name, out var accessDict) && accessDict.TryGetValue(req.User.Id, out var accessSet) ? accessSet : [];
                ulong actualLast, lastUnread;
                ulong lastKnown = req.Query.TryGetValue("last", out ulong lk) ? lk : 0;
                if (InvalidMailbox(req, out var mailbox))
                {
                    req.Status = 200;
                    actualLast = mailboxes.Max(LastInboxMessageId);
                    lastUnread = lastKnown == 0 ? 0 : mailboxes.Max(x => LastUnreadInboxMessageId(x, lastKnown));
                    //refresh on all mailboxes
                    foreach (var m in mailboxes)
                    {
                        if (IncomingListeners.TryGetValue(m, out var kv))
                            kv[req] = true;
                        else IncomingListeners[m] = new() { { req, true } };
                    }
                }
                else if ((!req.Query.TryGetValue("folder", out var folderName)) || folderName == "Inbox")
                {
                    actualLast = LastInboxMessageId(mailbox);
                    lastUnread = lastKnown == 0 ? 0 :LastUnreadInboxMessageId(mailbox, lastKnown);
                    //refresh for mailbox, icon for all others
                    foreach (var m in mailboxes)
                    {
                        if (IncomingListeners.TryGetValue(m, out var kv))
                            kv[req] = m == mailbox;
                        else IncomingListeners[m] = new() { { req, m == mailbox } };
                    }
                }
                else
                {
                    actualLast = LastInboxMessageId(mailbox);
                    lastUnread = lastKnown == 0 ? 0 : LastUnreadInboxMessageId(mailbox, lastKnown);
                    //icon for all mailboxes
                    foreach (var m in mailboxes)
                    {
                        if (IncomingListeners.TryGetValue(m, out var kv))
                            kv[req] = false;
                        else IncomingListeners[m] = new() { { req, false } };
                    }
                }
                //check if the event should already be called
                if (lastKnown != 0)
                {
                    if (lastUnread > lastKnown)
                        await req.EventMessage("icon");
                    if (actualLast > lastKnown)
                    {
                        await Task.Delay(2000); //wait a few seconds so it doesn't violently refresh in case something is broken
                        await req.EventMessage("refresh");
                    }
                }
                //keep alive
                await req.KeepEventAlive();
            } break;
            



            // 404
            default:
                req.CreatePage("Error");
                req.Status = 404;
                break;
        }
    }
}