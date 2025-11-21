using System.Web;
using uwap.WebFramework.Database;
using uwap.WebFramework.Accounts;
using uwap.WebFramework.Elements;
using uwap.WebFramework.Tools;
using static uwap.WebFramework.Mail.MailAuth;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public Task HandleSettings(Request req)
    {
        switch (req.Path)
        {
            // SETTINGS
            case "/settings":
            { CreatePage(req, "Mail settings", out var page, out var e);
                if (InvalidMailbox(req, out var mailbox))
                    break;
                page.Navigation.Add(new Button("Back", $".?mailbox={mailbox.Id}", "right"));
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("query.js"));
                page.Scripts.Add(new Script("settings.js"));
                page.Sidebar.Add(new ButtonElement("Mailboxes:", null, "."));
                foreach (var m in EnumerateAccessibleMailboxes(req))
                    page.Sidebar.Add(new ButtonElement(null, m.Address, $"settings?mailbox={m.Id}"));
                HighlightSidebar("settings", page, req);
                e.Add(new LargeContainerElement("Mail settings", mailbox.Address));
                page.AddError();
                e.Add(new ButtonElement("Folders", null, $"settings/folders?mailbox={mailbox.Id}"));
                e.Add(new ButtonElement("Authentication", null, $"settings/auth?mailbox={mailbox.Id}"));
                e.Add(new ButtonElement("Contacts", null, $"settings/contacts?mailbox={mailbox.Id}"));
                e.Add(new ContainerElement("Name", new TextBox("Enter a name...", mailbox.Name, "name-input", onEnter: "SaveName()", onInput: "NameChanged()")) { Button = new ButtonJS("Saved!", "SaveName()", id: "save-name") });
                e.Add(new ContainerElement("Footer", new TextArea("Enter a footer...", mailbox.Footer, "footer-input", 5, onInput: "FooterChanged()")) { Button = new ButtonJS("Saved!", "SaveFooter()", id: "save-footer") });
                e.Add(new ContainerElement("External images", new Checkbox("Show external image links", "external-images", mailbox.ShowExternalImageLinks) { OnChange = "SaveExternalImages()" }) { Button = new ButtonJS("Saved!", "SaveExternalImages()", id: "save-external-images")});
            } break;

            case "/settings/set-name":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!req.Query.TryGetValue("name", out var name))
                    throw new BadRequestSignal();
                if (name == "")
                    name = null;
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox) => mailbox.Name = name);
            } break;

            case "/settings/set-footer":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!req.Query.TryGetValue("footer", out var footer))
                    throw new BadRequestSignal();
                if (footer == "")
                    footer = null;
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox) => mailbox.Footer = footer);
            } break;

            case "/settings/set-external-images":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!req.Query.TryGetValue("value", out bool value))
                    throw new NotFoundSignal();
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox) => mailbox.ShowExternalImageLinks = value);
            } break;




            // SETTINGS > MAIL AUTHENTICATION
            case "/settings/auth":
            { CreatePage(req, "Mail authentication", out var page, out var e);
                if (InvalidMailbox(req, out var mailbox))
                    break;
                page.Navigation.Add(new Button("Back", $"../settings?mailbox={mailbox.Id}", "right"));
                page.Title = "Mail authentication";
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("../query.js"));
                page.Scripts.Add(new Script("auth.js"));
                page.Sidebar.Add(new ButtonElement("Mailboxes:", null, ".."));
                foreach (var m in EnumerateAccessibleMailboxes(req))
                    page.Sidebar.Add(new ButtonElement(null, m.Address, $"auth?mailbox={m.Id}"));
                HighlightSidebar("auth", page, req);
                e.Add(new LargeContainerElement("Mail authentication",
                [
                    new Paragraph(mailbox.Address),
                    new Paragraph("Selectors set what the lowest acceptable result is."),
                    new Paragraph("If a message does not satisfy these requirements, it will be placed in your spam folder.")
                ])
                { Button = new ButtonJS("Saved!", "Save()", id: "save") });
                page.AddError();
                var ar = mailbox.AuthRequirements;
                e.Add(new ContainerElement("Connection",
                [
                    new Checkbox("Require secure connection", "connection-secure", ar.Secure)
                    { OnChange = "Changed()" },
                    new Checkbox("Require PTR record", "connection-ptr", ar.PTR)
                    { OnChange = "Changed()" }
                ]));
                e.Add(new ContainerElement("SPF",
                [
                    new Selector("spf-min", ar.SPF.ToString(),
                        nameof(MailAuthVerdictSPF.HardFail),
                        nameof(MailAuthVerdictSPF.SoftFail),
                        nameof(MailAuthVerdictSPF.Unset),
                        nameof(MailAuthVerdictSPF.Pass))
                    { OnChange = "Changed()" }
                ]));
                e.Add(new ContainerElement("DKIM",
                [
                    new Selector("dkim-min", ar.DKIM.ToString(),
                        nameof(MailAuthVerdictDKIM.Fail),
                        nameof(MailAuthVerdictDKIM.Mixed),
                        nameof(MailAuthVerdictDKIM.Unset),
                        nameof(MailAuthVerdictDKIM.Pass))
                    { OnChange = "Changed()" }
                ]));
                e.Add(new ContainerElement("DMARC",
                [
                    new Checkbox("Always satisfied by DMARC pass", "dmarc-enough", ar.SatisfiedByDMARC)
                    { OnChange = "Changed()" },
                    new Selector("dmarc-min", ar.DMARC.ToString(),
                        nameof(MailAuthVerdictDMARC.FailWithReject),
                        nameof(MailAuthVerdictDMARC.FailWithQuarantine),
                        nameof(MailAuthVerdictDMARC.FailWithoutAction),
                        nameof(MailAuthVerdictDMARC.Unset),
                        nameof(MailAuthVerdictDMARC.Pass))
                    { OnChange = "Changed()" }
                ]));
            } break;

            case "/settings/auth/set":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!(req.Query.TryGetValue("connection-secure", out string? connectionSecureS) && req.Query.TryGetValue("connection-ptr", out string? connectionPtrS)
                    && req.Query.TryGetValue("spf-min", out string? spfMinS)
                    && req.Query.TryGetValue("dkim-min", out string? dkimMinS)
                    && req.Query.TryGetValue("dmarc-enough", out string? dmarcEnoughS) && req.Query.TryGetValue("dmarc-min", out string? dmarcMinS)
                    && bool.TryParse(connectionSecureS, out bool connectionSecure) && bool.TryParse(connectionPtrS, out bool connectionPtr)
                    && Enum.TryParse<MailAuthVerdictSPF>(spfMinS, out var spfMin)
                    && Enum.TryParse<MailAuthVerdictDKIM>(dkimMinS, out var dkimMin)
                    && bool.TryParse(dmarcEnoughS, out bool dmarcEnough) && Enum.TryParse<MailAuthVerdictDMARC>(dmarcMinS, out var dmarcMin)))
                    throw new BadRequestSignal();
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox) =>
                {
                    var ar = mailbox.AuthRequirements;
                    ar.Secure = connectionSecure;
                    ar.PTR = connectionPtr;
                    ar.SPF = spfMin;
                    ar.DKIM = dkimMin;
                    ar.SatisfiedByDMARC = dmarcEnough;
                    ar.DMARC = dmarcMin;
                });
            } break;




            // SETTINGS > CONTACTS
            case "/settings/contacts":
            { CreatePage(req, "Mail", out var page, out var e);
                if (InvalidMailbox(req, out var mailbox))
                    break;
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("../query.js"));
                page.Sidebar.Add(new ButtonElement("Mailboxes:", null, ".."));
                foreach (var m in EnumerateAccessibleMailboxes(req))
                    page.Sidebar.Add(new ButtonElement(null, m.Address, $"contacts?mailbox={m.Id}"));
                HighlightSidebar("contacts", page, req, "email");
                if (req.Query.TryGetValue("email", out string? email))
                {
                    //edit
                    if (!mailbox.Contacts.TryGetValue(email, out var contact))
                        throw new NotFoundSignal();
                    page.Title = "Edit contact";
                    page.Navigation.Add(new Button("Back", $"contacts?mailbox={mailbox.Id}", "right"));
                    page.Scripts.Add(new Script("contacts-edit.js"));
                    e.Add(new LargeContainerElement("Edit contact",
                    [
                        new Paragraph(email),
                        new TextBox("Enter a name...", contact.Name, "name", onEnter: "Save()", onInput: "Changed()", autofocus: true),
                        new Checkbox("Favorite", "favorite", contact.Favorite) { OnChange = "Changed()" }
                    ]) { Buttons =
                    [
                        new ButtonJS("Delete", "Delete()", "red", id: "del"),
                        new ButtonJS("Saved!", "Save()", id: "save")
                    ] });
                    page.AddError();
                }
                else
                {
                    //add+list
                    page.Title = "Mail contacts";
                    page.Navigation.Add(new Button("Back", $"../settings?mailbox={mailbox.Id}", "right"));
                    page.Scripts.Add(new Script("contacts.js"));
                    e.Add(new LargeContainerElement("Mail contacts", new Paragraph(mailbox.Address)));
                    e.Add(new ContainerElement("Add contact",
                    [
                        new TextBox("Enter an address...", null, "email", TextBoxRole.Email, onEnter: "Add()", onInput: "Changed()", autofocus: true),
                        new TextBox("Enter a name...", null, "name", onEnter: "Add()", onInput: "Changed()"),
                        new Checkbox("Favorite", "favorite") { OnChange = "Changed()" }
                    ]) { Button = new ButtonJS("Add", "Add()", "green", id: "save") });
                    page.AddError();
                    Search<KeyValuePair<string, MailContact>> search = new(mailbox.Contacts, null);
                    foreach (var contactKV in search.Sort(x => !x.Value.Favorite, x => x.Value.Name))
                        e.Add(new ButtonElement($"{(contactKV.Value.Favorite ? "[*] " : "")}{contactKV.Value.Name}", contactKV.Key, $"contacts?mailbox={mailbox.Id}&email={HttpUtility.UrlEncode(contactKV.Key)}"));
                }
            } break;

            case "/settings/contacts/set":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!(req.Query.TryGetValue("email", out string? email)
                    && req.Query.TryGetValue("name", out string? name)
                    && req.Query.TryGetValue("favorite", out string? favoriteS)
                    && bool.TryParse(favoriteS, out bool favorite)))
                    throw new BadRequestSignal();
                if (!AccountManager.CheckMailAddressFormat(email))
                    throw new HttpStatusSignal(418);
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox) => mailbox.Contacts[email] = new(name, favorite));
            } break;

            case "/settings/contacts/delete":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!req.Query.TryGetValue("email", out string? email))
                    throw new BadRequestSignal();
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox) => mailbox.Contacts.Remove(email));
            } break;




            // SETTINGS > FOLDERS
            case "/settings/folders":
            { CreatePage(req, "Mail folders", out var page, out var e);
                if (InvalidMailbox(req, out var mailbox))
                    break;
                page.Navigation.Add(new Button("Back", $"../settings?mailbox={mailbox.Id}", "right"));
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("../query.js"));
                page.Scripts.Add(new Script("folders.js"));
                page.Sidebar.Add(new ButtonElement("Mailboxes:", null, ".."));
                foreach (var m in EnumerateAccessibleMailboxes(req))
                    page.Sidebar.Add(new ButtonElement(null, m.Address, $"folders?mailbox={m.Id}"));
                HighlightSidebar("folders", page, req);
                e.Add(new LargeContainerElement("Mail folders", new List<IContent> { new Paragraph(mailbox.Address), new Paragraph("Warning: Deleting a folder will delete all of the messages within it!") }));
                e.Add(new ContainerElement("New folder", new TextBox("Enter a name...", null, "name", onEnter: "Create()", autofocus: true)) { Button = new ButtonJS("Create", "Create()", "green")});
                page.AddError();
                foreach (var f in SortFolders(mailbox.Folders.Keys))
                    e.Add(DefaultFolders.Contains(f)
                        ? new ContainerElement(null, f)
                        : new ContainerElement(null, f)
                        {
                            Button = new ButtonJS("Delete", $"Delete('{HttpUtility.UrlEncode(f)}', '{f.ToId()}')",
                                "red", id: f.ToId())
                        });
            } break;

            case "/settings/folders/create":
            { POST(req);
                if (InvalidMailbox(req, out var readMailbox))
                    break;
                if (!req.Query.TryGetValue("name", out var name))
                    throw new BadRequestSignal();
                if (readMailbox.Folders.ContainsKey(name))
                    throw new HttpStatusSignal(409);
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox) => mailbox.Folders[name] = []);
            } break;

            case "/settings/folders/delete":
            { POST(req);
                if (InvalidMailboxOrFolder(req, out var readMailbox, out _, out var folderName))
                    break;
                Mailboxes.Transaction(readMailbox.Id, (ref Mailbox mailbox, ref List<IFileAction> actions) =>
                {
                    foreach (var messageId in mailbox.Folders[folderName])
                    {
                        mailbox.DeleteFileIfExists($"{messageId}/html", actions);
                        mailbox.DeleteFileIfExists($"{messageId}/text", actions);
                        int attachmentId = 0;
                        while (mailbox.DeleteFileIfExists($"{messageId}/{attachmentId}", actions))
                            attachmentId++;
                        
                        mailbox.Messages.Remove(messageId);
                    }
                    mailbox.Folders.Remove(folderName);
                });
            } break;
            



            // 404
            default:
                req.CreatePage("Error");
                req.Status = 404;
                break;
        }

        return Task.CompletedTask;
    }
}