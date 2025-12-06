using uwap.WebFramework.Accounts;
using uwap.WebFramework.Elements;
using uwap.WebFramework.Responses;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public async Task<IResponse> HandleManage(Request req)
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
                    var mailbox = await Mailboxes.GetByIdNullableAsync(mailboxId);
                    if (mailbox == null)
                    {
                        //mailbox doesn't exist
                        e.Add(new LargeContainerElement("Error", "This mailbox doesn't exist!", "red"));
                        return new LegacyPageResponse(page, req);
                    }
                    //manage mailbox
                    page.Navigation.Add(new Button("Back", "manage", "right"));
                    page.Title = "Manage " + mailbox.Address;
                    page.Scripts.Add(Presets.SendRequestScript);
                    page.Scripts.Add(new Script("query.js"));
                    page.Scripts.Add(new Script("manage-mailbox.js"));
                    page.Sidebar.Add(new ButtonElement("Mailboxes:", null, "manage"));
                    foreach (var m in (await Mailboxes.ListAllAsync()).OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                        page.Sidebar.Add(new ButtonElement(null, m.Address, $"manage?mailbox={m.Id}"));
                    HighlightSidebar("manage", page, req);
                    e.Add(new LargeContainerElement("Manage " + mailbox.Address));
                    e.Add(new ButtonElementJS("Delete mailbox", null, "Delete()", id: "deleteButton"));
                    e.Add(new ContainerElement("Add access:", new TextBox("Enter a username...", null, "username", onEnter: "Add()")) { Button = new ButtonJS("Add", "Add()", "green") });
                    page.AddError();
                    if (mailbox.AllowedUserIds.Any(x => x.Value.Count != 0))
                        foreach (var userTableKV in mailbox.AllowedUserIds)
                        {
                            UserTable userTable = UserTable.Import(userTableKV.Key);
                            foreach (var userId in userTableKV.Value)
                            {
                                var u = await userTable.GetByIdNullableAsync(userId);
                                if (u != null)
                                    e.Add(new ContainerElement(u.Username, u.Id) { Button = new ButtonJS("Remove", $"Remove('{userTable.Name}:{userId}')", "red") });
                            }
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
                    var mailboxes = await Mailboxes.ListAllAsync();
                    e.Add(new ContainerElement("Create mailbox:", new TextBox("Enter an address...", null, "address", onEnter: "Create()")) { Button = new ButtonJS("Create", "Create()", "green") });
                    page.AddError();
                    foreach (Mailbox m in mailboxes.OrderBy(x => x.Address.After('@')).ThenBy(x => x.Address.Before('@')))
                        e.Add(new ButtonElement(m.Address, m.Name ?? "", $"manage?mailbox={m.Id}"));
                    if (mailboxes.Count == 0)
                        e.Add(new ContainerElement("No mailboxes!", "", "red"));
                }
                return new LegacyPageResponse(page, req);
            }

            case "/manage/create-mailbox":
            { POST(req);
                req.ForceAdmin(false);
                var address = req.Query.GetOrThrow("address");
                if (!AccountManager.CheckMailAddressFormat(address))
                    return new TextResponse("format");
                else if (await Mailboxes.AddressIndex.GetAsync(address) != null)
                    return new TextResponse("exists");
                else
                {
                    Mailbox mailbox = new(address);
                    await Mailboxes.CreateAsync(10, mailbox);
                    return new TextResponse("mailbox=" + mailbox.Id);
                }
            }

            case "/manage/delete-mailbox":
            { POST(req);
                req.ForceAdmin(false);
                var mailboxId = req.Query.GetOrThrow("mailbox");
                await Mailboxes.DeleteAsync(mailboxId);
                return StatusResponse.Success;
            }

            case "/manage/add-access":
            { POST(req);
                req.ForceAdmin(false);
                var mailboxId = req.Query.GetOrThrow("mailbox");
                var usernameCombined = req.Query.GetOrThrow("username");
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
                    return new TextResponse("invalid");
                User? user = await userTable.FindByUsernameAsync(username);
                if (user == null)
                    return new TextResponse("invalid");
                
                await Mailboxes.TransactionAsync(mailboxId, t =>
                {
                    var set = t.Value.AllowedUserIds.GetValueOrAdd(userTable.Name, () => []);
                    set.Add(user.Id);
                });
                return new TextResponse("ok");
            }

            case "/manage/remove-access":
            { POST(req);
                req.ForceAdmin(false);
                var mailboxId = req.Query.GetOrThrow("mailbox");
                var idCombined = req.Query.GetOrThrow("id");
                int colon = idCombined.IndexOf(':');
                if (colon == -1)
                    return StatusResponse.BadRequest;
                string userTableName = idCombined.Remove(colon);
                string userId = idCombined.Remove(0, colon + 1);
                
                await Mailboxes.TransactionAsync(mailboxId, t =>
                {
                    t.Value.AllowedUserIds.RemoveAndClean(userTableName, userId);
                });
                return StatusResponse.Success;
            }
            



            // 404
            default:
                return StatusResponse.NotFound;
        }
    }
}