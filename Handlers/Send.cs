using System.Web;
using MimeKit;
using uwap.WebFramework.Database;
using uwap.WebFramework.Accounts;
using uwap.WebFramework.Elements;
using uwap.WebFramework.Mail;
using uwap.WebFramework.Tools;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public async Task HandleSend(Request req)
    {
        switch (req.Path)
        {
            case "/send":
            { CreatePage(req, "Mail", out var page, out var e);
                if (InvalidMailbox(req, out var mailbox))
                    break;
                string? to, subject, text;
                if (mailbox.Messages.TryGetValue(0, out var message))
                {
                    to = string.Join(", ", message.To.Select(x => x.Address));
                    if (to == "")
                        to = null;
                    subject = message.Subject;
                    if (subject == "")
                        subject = null;
                    text = mailbox.GetFileText("0/text");
                    if (text == "")
                        text = null;
                }
                else
                {
                    message = null;
                    to = null; subject = null;
                    if (mailbox.Footer != null)
                        text = "\n\n" + mailbox.Footer;
                    else text = null;
                }
                page.Navigation.Add(new Button("Back", $".?mailbox={mailbox.Id}", "right"));
                page.Title = message?.InReplyToId == null ? "Send an email" : "Reply";
                page.HideFooter = true;
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("query.js"));
                page.Scripts.Add(new Script("send.js"));
                page.Sidebar.Add(new ButtonElement("Mailboxes:", null, "."));
                foreach (var m in EnumerateAccessibleMailboxes(req))
                {
                    page.Sidebar.Add(new ButtonElement(null, m.Address, $"send?mailbox={m.Id}"));
                }
                HighlightSidebar("send", page, req);
                List<IContent> headingContent =
                [
                    new Paragraph($"From: {mailbox.Address}{(mailbox.Name == null ? "" : $" ({mailbox.Name})")}")
                ];
                if (message is { InReplyToId: not null })
                {
                    headingContent.Add(new Paragraph($"To: {to}"));
                    headingContent.Add(new Paragraph($"Subject: {subject}"));
                } 
                e.Add(new LargeContainerElement((message == null || message.InReplyToId == null) ? "Send an email" : "Reply", headingContent) { Button = new ButtonJS("Send", "Send()", "green", id: "send") });
                e.Add(Presets.ErrorElement);
                e.Add(new ContainerElement(null, "Draft:") { Buttons =
                [
                    new ButtonJS("Preview", "GoToPreview()"),
                    new ButtonJS("Saved!", "Save()", id: "save"),
                    new ButtonJS("Discard", "Discard()", "red", id: "discard")
                ]});
                List<IContent> inputs = [];
                if (message == null || message.InReplyToId == null)
                {
                    inputs.Add(new TextBox("Recipient(s)...", to, "to", TextBoxRole.Email, onInput: "MessageChanged()"));
                    inputs.Add(new TextBox("Subject...", subject, "subject", onInput: "MessageChanged()"));
                }
                inputs.Add(new TextArea("Message...", text, "text", classes: "grow", styles: "min-height: 10rem", autofocus: true, onInput: "MessageChanged()"));
                e.Add(new LargeContainerElement(null, inputs));
                int attachmentCount = message == null ? 0 : message.Attachments.Count;
                e.Add(new ContainerElement(null, "More:") { Buttons = [new ButtonJS($"Attachments ({attachmentCount})", "GoToAttachments()"), ..message == null || message.InReplyToId == null ? (IEnumerable<IButton>)[new ButtonJS("Contacts", "GoToContacts()")] : []] });
            } break;

            case "/send/save-draft":
            { POST(req);
                if (req.IsForm)
                    throw new BadRequestSignal();
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                
                string text = await req.GetBodyText();
                
                if (readMailbox.Messages.TryGetValue(0, out var readMessage) && readMessage.InReplyToId != null)
                {
                    Mailboxes.Transaction(readMailbox.Id, (ref Mailbox _, ref List<IFileAction> actions)
                        => actions.Add(new SetFileAction("0/text", path => File.WriteAllText(path, text))));
                }
                else
                {
                    if (!(req.Query.TryGetValue("to", out var toString) && req.Query.TryGetValue("subject", out var subject)))
                        throw new BadRequestSignal();
                    var to = toString.Split(',', ';', ' ').Where(x => x != "").ToList();
                    if (to.Any(x => !AccountManager.CheckMailAddressFormat(x)))
                    {
                        await req.Write("invalid-to");
                        break;
                    }
                    Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox, ref List<IFileAction> actions) =>
                    {
                        var contacts = mailbox.Contacts;
                        if (mailbox.Messages.TryGetValue(0, out var message))
                        {
                            message.To = to.Select(x => new MailAddress(x, contacts.TryGetValue(x, out var contact) ? contact.Name : x)).ToList();
                            message.Subject = subject.Trim();
                        }
                        else
                        {
                            mailbox.Messages[0] = new(new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address), to.Select(x => new MailAddress(x, contacts.TryGetValue(x, out var contact) ? contact.Name : x)).ToList(), subject.Trim(), null);
                        }
                    
                        actions.Add(new SetFileAction("0/text", path => File.WriteAllText(path, text)));
                    });
                }

                await req.Write("ok");
            } break;
            
            case "/send/delete-draft":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!readMailbox.Messages.ContainsKey(0))
                    break;
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox, ref List<IFileAction> actions) =>
                {
                    mailbox.Messages.Remove(0);
                    mailbox.DeleteFileIfExists("0/html", actions);
                    mailbox.DeleteFileIfExists("0/text", actions);
                    int attachmentId = 0;
                    while (mailbox.DeleteFileIfExists($"0/{attachmentId}", actions))
                        attachmentId++;
                });
            } break;
                
            case "/send/send-draft":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!readMailbox.Messages.TryGetValue(0, out var readMessage))
                {
                    await req.Write("no-draft");
                    break;
                }
                if (readMessage.Subject == "")
                {
                    await req.Write("invalid-subject");
                    break;
                }
                if (readMessage.To.Count == 0)
                {
                    await req.Write("invalid-to");
                    break;
                }
                string text = readMailbox.GetFileText("0/text")?.Trim() ?? "";
                if (text == "")
                {
                    await req.Write("invalid-text");
                    break;
                }
                
                var messageId = Mailboxes.TransactionAndGet(readMailbox.Id, (ref Mailbox mailbox, ref List<IFileAction> actions) =>
                {
                    var message = readMailbox.Messages[0];
                    ulong messageId = (ulong)message.TimestampUtc.Ticks;
                    while (mailbox.Messages.ContainsKey(messageId))
                        messageId++;
                    
                    message.TimestampUtc = DateTime.UtcNow;
                    message.From = new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address);
                    string htmlPart = AddHTML(text);
                    string textPart = RemoveHTML(htmlPart);
                    actions.Add(new SetFileAction($"{messageId}/html", path => File.WriteAllText(path, htmlPart)));
                    actions.Add(new SetFileAction($"{messageId}/text", path => File.WriteAllText(path, textPart)));
                    MailGen msg = new(new(message.From.Name, message.From.Address), message.To.Select(x => new MailboxAddress(x.Name, x.Address)), message.Subject, textPart, htmlPart);
                    if (message.InReplyToId != null)
                        msg.IsReplyToMessageId = message.InReplyToId;
                    int counter = 0;
                    foreach (var attachment in message.Attachments)
                    {
                        msg.Attachments.Add(new(string.IsNullOrEmpty(attachment.Name) ? "Unknown name" : attachment.Name, attachment.MimeType, mailbox.GetFileBytes($"0/{counter}") ?? []));
                        actions.Add(new MoveFileAction($"0/{counter}", $"{messageId}/{counter}"));
                        counter++;
                    }
                    var result = MailManager.Out.Send(msg, out var messageIds);
                    message.MessageId = string.Join('\n', messageIds);
                    var log = message.Log;
                    if (result.Internal.Count != 0)
                    {
                        log.Add("Internal:");
                        foreach (var l in result.Internal)
                            log.Add(l.Key.Address + ": " + l.Value);
                    }
                    if (result.FromSelf != null)
                    {
                        log.Add("From the server directly: " + result.FromSelf.ResultType.ToString());
                        foreach (string l in result.FromSelf.ConnectionLog)
                            log.Add(l);
                    }
                    if (result.FromBackup != null)
                    {
                        log.Add("From the backup sender: " + result.FromBackup.ResultType.ToString());
                        foreach (string l in result.FromBackup.ConnectionLog)
                            log.Add(l);
                    }
                    mailbox.Messages.Remove(0);
                    mailbox.Messages[messageId] = message;
                    mailbox.Folders["Sent"].Add(messageId);
                    return messageId;
                });
                await req.Write("message=" + messageId);
            } break;




            // SEND > ATTACHMENTS
            case "/send/attachments":
            { CreatePage(req, "Mail", out var page, out var e);
                if (InvalidMailbox(req, out var mailbox))
                    break;
                page.Navigation.Add(new Button("Back", $"../send?mailbox={mailbox.Id}", "right"));
                page.Title = "Send an email";
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("../query.js"));
                page.Scripts.Add(new Script("attachments.js"));
                page.Sidebar.Add(new ButtonElement("Mailboxes:", null, "."));
                foreach (var m in EnumerateAccessibleMailboxes(req))
                    page.Sidebar.Add(new ButtonElement(null, m.Address, $"attachments?mailbox={m.Id}"));
                HighlightSidebar("attachments", page, req);
                e.Add(new LargeContainerElement("Send an email", $"From: {mailbox.Address}{(mailbox.Name == null ? "" : $" ({mailbox.Name})")}", id: "e1"));
                e.Add(new ContainerElement("Add attachment:", new FileSelector("update-file")) { Button = new ButtonJS("Add", "Upload()", "green", id: "uploadButton") });
                page.AddError();
                int attachmentCount = 0;
                while (mailbox.ContainsFile($"0/{attachmentCount}"))
                    attachmentCount++;
                if (mailbox.Messages.TryGetValue(0, out var message))
                {
                    int counter = 0;
                    foreach (var attachment in message.Attachments)
                    {
                        e.Add(new ContainerElement(null, attachment.Name ?? "No file name", "overflow") { Button = new ButtonJS("Remove", $"Delete('{counter}')", "red") });
                        counter++;
                    }
                }
            } break;

            case "/send/attachments/upload":
            { POST(req);
                if (!req.IsForm || req.Files.Count != 1)
                    throw new BadRequestSignal();
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                var file = req.Files[0];
                var temp = Path.GetTempFileName();
                if (!file.Download(temp, 10485760))
                {
                    req.Status = 413;
                    File.Delete(temp);
                    break;
                }

                try
                {
                    Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox, ref List<IFileAction> actions) =>
                    {
                        if (mailbox.Messages.TryGetValue(0, out var message))
                        {
                            message.From = new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address);
                        }
                        else
                        {
                            message = new(new MailAddress(mailbox.Address, mailbox.Name ?? mailbox.Address), [], "", null);
                            mailbox.Messages[0] = message;
                        }
                        int attachmentId = message.Attachments.Count;
                        // ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                        message.Attachments.Add(new(file.FileName?.Trim().HtmlSafe(), file.ContentType?.Trim().HtmlSafe()));
                        // ReSharper restore ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                        actions.Add(new SetFileAction($"0/{attachmentId}", path => File.Move(temp, path)));
                    });
                }
                finally
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
            } break;

            case "/send/attachments/delete":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!(req.Query.TryGetValue("attachment", out var attachmentId) && int.TryParse(attachmentId, out var a) && a >= 0))
                    throw new BadRequestSignal();
                if (!(readMailbox.Messages.TryGetValue(0, out var readMessage) && a < readMessage.Attachments.Count))
                    throw new NotFoundSignal();
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox, ref List<IFileAction> actions) =>
                {
                    if (!mailbox.Messages.TryGetValue(0, out var message))
                        return;
                    
                    message.Attachments.RemoveAt(a);
                    if (a == message.Attachments.Count)
                    {
                        actions.Add(new DeleteFileAction($"0/{a}"));
                    }
                    else
                    {
                        a++;
                        while (mailbox.ContainsFile($"0/{a}"))
                        {
                            actions.Add(new MoveFileAction($"0/{a}", $"0/{a - 1}"));
                            a++;
                        }
                    }
                });
            } break;




            // SEND > CONTACTS
            case "/send/contacts":
            { CreatePage(req, "Mail", out var page, out var e);
                if (InvalidMailbox(req, out var mailbox))
                    break;
                string? query = req.Query.TryGet("search");
                page.Navigation.Add(new Button("Back", $"../send{(query == null ? "" : "/contacts")}?mailbox={mailbox.Id}", "right"));
                page.Title = "Send an email";
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("../query.js"));
                page.Scripts.Add(new Script("contacts.js"));
                page.Sidebar.Add(new ButtonElement("Mailboxes:", null, "."));
                foreach (var m in EnumerateAccessibleMailboxes(req))
                    page.Sidebar.Add(new ButtonElement(null, m.Address, $"contacts?mailbox={m.Id}"));
                HighlightSidebar("contacts", page, req, "search");
                if (!mailbox.Messages.ContainsKey(0))
                {
                    e.Add(new LargeContainerElement("No draft found!", "", "red"));
                    break;
                }
                e.Add(new LargeContainerElement("Send an email", $"From: {mailbox.Address}{(mailbox.Name == null ? "" : $" ({mailbox.Name})")}", id: "e1"));
                e.Add(new ContainerElement(null, new TextBox("Search...", query, "search", onEnter: $"Search()", autofocus: true)));
                page.AddError();
                if (mailbox.Contacts.Count == 0)
                    e.Add(new ContainerElement("No contacts found!", "", "red"));
                else
                {
                    Search<KeyValuePair<string, MailContact>> search = new(mailbox.Contacts, query);
                    if (query != null)
                    {
                        search.Find(x => x.Value.Name);
                        search.Find(x => x.Key);
                    }
                    foreach (var contactKV in search.Sort(x => !x.Value.Favorite, x => x.Value.Name))
                        e.Add(new ButtonElementJS($"{(contactKV.Value.Favorite ? "[*] " : "")}{contactKV.Value.Name}", contactKV.Key, $"AddContact('{HttpUtility.UrlEncode(contactKV.Key)}')"));
                }
            } break;

            case "/send/contacts/add":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!req.Query.TryGetValue("email", out string? email))
                    throw new BadRequestSignal();
                if (!readMailbox.Messages.TryGetValue(0, out var readMessage))
                    throw new NotFoundSignal();
                if (readMessage.InReplyToId != null)
                    throw new ForbiddenSignal();
                if (readMessage.To.All(x => x.Address != email))
                    Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox) =>
                    {
                        if (mailbox.Messages.TryGetValue(0, out var message))
                            message.To.Add(new(email, mailbox.Contacts.TryGetValue(email, out var contact) ? contact.Name : email));
                    });
            } break;




            // SEND > PREVIEW
            case "/send/preview":
            { CreatePage(req, "Mail", out var page, out var e);
                if (InvalidMailbox(req, out var mailbox))
                    break;
                page.Navigation.Add(new Button("Back", $"../send?mailbox={mailbox.Id}", "right"));
                page.Title = "Preview draft";
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("../query.js"));
                page.Scripts.Add(new Script("preview.js"));
                page.Sidebar.Add(new ButtonElement("Mailboxes:", null, "."));
                foreach (var m in EnumerateAccessibleMailboxes(req))
                    page.Sidebar.Add(new ButtonElement(null, m.Address, $"preview?mailbox={m.Id}"));
                HighlightSidebar("preview", page, req);
                if (!mailbox.Messages.TryGetValue(0, out var message))
                {
                    e.Add(new LargeContainerElement("No draft found!", "", "red"));
                    break;
                }
                List<IContent> headingContents = [];
                if (message.InReplyToId != null)
                    headingContents.Add(new Paragraph($"This is a reply to another email (<a href=\"javascript:\" id=\"find\" onclick=\"FindOriginal('{HttpUtility.UrlEncode(message.InReplyToId).HtmlValueSafe()}')\">find</a>).") {Unsafe = true});
                headingContents.Add(new Paragraph("From: " + message.From.FullString));
                if (message.To.Count != 0)
                    foreach (var to in message.To)
                        headingContents.Add(new Paragraph("To: " + to.FullString));
                else headingContents.Add(new Paragraph("To: [no recipients]"));
                headingContents.Add(new Paragraph("Subject: " + (message.Subject == "" ? "[no subject]" : message.Subject)));
                e.Add(new LargeContainerElement("Preview draft", headingContents) { Button = new ButtonJS("Send", "Send()", "green", id: "send") });
                page.AddError();

                string? c = mailbox.GetFileText("0/text")?.Map(t => AddHTML(t.Trim()));
                List<IContent>? textContents = c == null ? null : ReadHTML(c, true);
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
                            // ReSharper disable once ConstantNullCoalescingCondition
                            new Paragraph("File: " + attachment.Name ?? "Unknown name"),
                            // ReSharper disable once ConstantNullCoalescingCondition
                            new Paragraph("Type: " + attachment.MimeType ?? "Unknown type"),
                            new Paragraph("Size: " + FileSizeString(mailbox.TryGetFileInfo($"0/{attachmentId}", out var fileData) ? fileData.Size : 0L))
                        ])
                        { Buttons =
                            [
                                new Button("View", $"../attachment?mailbox={mailbox.Id}&message=0&attachment={attachmentId}&download=false", newTab: true),
                                new Button("Download", $"../attachment?mailbox={mailbox.Id}&message=0&attachment={attachmentId}&download=true", newTab: true)
                            ]
                        });
                        attachmentId++;
                    }
                }
            } break;
            



            // 404
            default:
                req.CreatePage("Error");
                req.Status = 404;
                break;
        }
    }
}