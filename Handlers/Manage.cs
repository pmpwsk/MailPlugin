using System.Web;
using uwap.WebFramework.Accounts;
using uwap.WebFramework.Elements;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public async Task HandleManage(Request req)
    {
        switch (req.Path)
        {
            // MANAGE MAILBOXES
            case "/manage":
            { CreatePage(req, "Mail", out var page, out var e);
                req.ForceAdmin();
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
                    page.Navigation.Add(new Button("Back", "manage", "right"));
                    page.Title = "Manage " + mailbox.Address;
                    page.Scripts.Add(Presets.SendRequestScript);
                    page.Scripts.Add(new Script("query.js"));
                    page.Scripts.Add(new Script("manage-mailbox.js"));
                    page.Sidebar.Add(new ButtonElement("Mailboxes:", null, "manage"));
                    foreach (var m in Mailboxes.Select(x => x.Value).OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                        page.Sidebar.Add(new ButtonElement(null, m.Address, $"manage?mailbox={m.Id}"));
                    HighlightSidebar("manage", page, req);
                    e.Add(new LargeContainerElement("Manage " + mailbox.Address));
                    e.Add(new ButtonElementJS("Delete mailbox", null, "Delete()", id: "deleteButton"));
                    e.Add(new ContainerElement("Add access:", new TextBox("Enter a username...", null, "username", onEnter: "Add()")) { Button = new ButtonJS("Add", "Add()", "green") });
                    Presets.AddError(page);
                    if (mailbox.AllowedUserIds.Any(x => x.Value.Count != 0))
                        foreach (var userTableKV in mailbox.AllowedUserIds)
                        {
                            UserTable userTable = UserTable.Import(userTableKV.Key);
                            foreach (var userId in userTableKV.Value)
                                if (userTable.TryGetValue(userId, out User? u))
                                    e.Add(new ContainerElement(u.Username, u.Id) { Button = new ButtonJS("Remove", $"Remove('{userTable.Name}:{userId}')", "red") });
                        }
                    else e.Add(new ContainerElement("No allowed accounts!", "", "red"));
                }
                else
                {
                    //list mailboxes to manage them, and offer to create a new one
                    page.Navigation.Add(new Button("Back", ".", "right"));
                    page.Title = "Manage mailboxes";
                    page.Scripts.Add(Presets.SendRequestScript);
                    page.Scripts.Add(new Script("manage.js"));
                    e.Add(new LargeContainerElement("Manage mailboxes", ""));
                    var mailboxes = Mailboxes.Select(x => x.Value).OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@'));
                    e.Add(new ContainerElement("Create mailbox:", new TextBox("Enter an address...", null, "address", onEnter: "Create()")) { Button = new ButtonJS("Create", "Create()", "green") });
                    Presets.AddError(page);
                    if (mailboxes.Any())
                        foreach (Mailbox m in mailboxes)
                            e.Add(new ButtonElement(m.Address, m.Name ?? "", $"manage?mailbox={m.Id}"));
                    else e.Add(new ContainerElement("No mailboxes!", "", "red"));
                }
            } break;

            case "/manage/create-mailbox":
            { POST(req);
                req.ForceAdmin(false);
                if (!req.Query.TryGetValue("address", out string? address))
                    throw new BadRequestSignal();
                if (!AccountManager.CheckMailAddressFormat(address))
                    await req.Write("format");
                else if (Mailboxes.MailboxByAddress.ContainsKey(address))
                    await req.Write("exists");
                else
                {
                    string id;
                    do id = Parsers.RandomString(10);
                    while (Mailboxes.ContainsKey(id));
                    Mailbox mailbox = new(id, address);
                    Mailboxes[id] = mailbox;
                    MailboxTable.AddToAccelerators(mailbox, Mailboxes.UserAllowedMailboxes, Mailboxes.MailboxByAddress);
                    Directory.CreateDirectory($"../MailPlugin.Mailboxes/{mailbox.Id}");
                    await req.Write("mailbox=" + id);
                }
            } break;

            case "/manage/delete-mailbox":
            { POST(req);
                req.ForceAdmin(false);
                if (!req.Query.TryGetValue("mailbox", out string? mailboxId))
                    throw new BadRequestSignal();
                if (Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                {
                    Mailboxes.Delete(mailboxId);
                    MailboxTable.RemoveFromAccelerators(mailbox, Mailboxes.UserAllowedMailboxes, Mailboxes.MailboxByAddress);
                    if (Directory.Exists($"../MailPlugin.Mailboxes/{mailbox.Id}"))
                        Directory.Delete($"../MailPlugin.Mailboxes/{mailbox.Id}", true);
                }
            } break;

            case "/manage/add-access":
            { POST(req);
                req.ForceAdmin(false);
                if ((!req.Query.TryGetValue("mailbox", out var mailboxId)) || !req.Query.TryGetValue("username", out var usernameCombined))
                    throw new BadRequestSignal();
                UserTable? userTable = req.UserTable;
                string username = usernameCombined;
                int colon = usernameCombined.IndexOf(':');
                if (colon != -1)
                {
                    string userTableName = usernameCombined.Remove(colon);
                    userTable = Server.Config.Accounts.UserTables.Values.FirstOrDefault(x => x.Name == userTableName);
                    username = usernameCombined.Remove(0, colon + 1);
                }
                if (userTable == null)
                {
                    await req.Write("invalid");
                    break;
                }
                User? user = userTable.FindByUsername(username);
                if (user == null)
                {
                    await req.Write("invalid");
                    break;
                }
                if (!Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                    throw new NotFoundSignal();
                mailbox.Lock();
                if (!mailbox.AllowedUserIds.TryGetValue(userTable.Name, out var userTableDict))
                {
                    userTableDict = [];
                    mailbox.AllowedUserIds[userTable.Name] = userTableDict;
                }
                userTableDict.Add(user.Id);
                if (!Mailboxes.UserAllowedMailboxes.TryGetValue(userTable.Name, out var userTableDict2))
                {
                    userTableDict2 = [];
                    Mailboxes.UserAllowedMailboxes[userTable.Name] = userTableDict2;
                }
                if (!userTableDict2.TryGetValue(user.Id, out var userSet))
                {
                    userSet = [];
                    userTableDict2[user.Id] = userSet;
                }
                userSet.Add(mailbox);
                mailbox.UnlockSave();
                await req.Write("ok");
            } break;

            case "/manage/remove-access":
            { POST(req);
                req.ForceAdmin(false);
                if ((!req.Query.TryGetValue("mailbox", out var mailboxId)) || !req.Query.TryGetValue("id", out var idCombined))
                    throw new BadRequestSignal();
                int colon = idCombined.IndexOf(':');
                if (colon == -1)
                    throw new BadRequestSignal();
                string userTableName = idCombined.Remove(colon);
                string userId = idCombined.Remove(0, colon + 1);
                if (!Mailboxes.TryGetValue(mailboxId, out Mailbox? mailbox))
                    throw new NotFoundSignal();
                mailbox.Lock();
                if (mailbox.AllowedUserIds.TryGetValue(userTableName, out var userTableSet))
                {
                    userTableSet.Remove(userId);
                    if (userTableSet.Count == 0)
                        mailbox.AllowedUserIds.Remove(userTableName);
                }
                if (Mailboxes.UserAllowedMailboxes.TryGetValue(userTableName, out var userTableDict))
                {
                    if (userTableDict.TryGetValue(userId, out var userSet))
                    {
                        userSet.Remove(mailbox);
                        if (userSet.Count == 0)
                            userTableDict.Remove(userId);
                    }
                    if (userTableDict.Count == 0)
                        Mailboxes.UserAllowedMailboxes.Remove(userTableName);
                }
                mailbox.UnlockSave();
            } break;
            



            // 404
            default:
                req.CreatePage("Error");
                req.Status = 404;
                break;
        }
    }
}