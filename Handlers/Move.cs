using System.Web;
using uwap.WebFramework.Elements;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public Task HandleMove(Request req)
    {
        switch (req.Path)
        {
            // MOVE MAIL
            case "/move":
            { CreatePage(req, "Move", out var page, out var e);
                if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out var message, out var messageId, out _, out var folderName))
                    break;
                if (folderName == "Sent")
                    throw new BadRequestSignal();
                page.Navigation.Add(new Button("Back", $".?mailbox={mailbox.Id}&folder={folderName}&message={messageId}", "right"));
                page.Scripts.Add(Presets.SendRequestScript);
                page.Scripts.Add(new Script("query.js"));
                page.Scripts.Add(new Script("move.js"));
                e.Add(new LargeContainerElement("Moving", message.Subject));
                page.AddError();
                foreach (var f in SortFolders(mailbox.Folders.Keys))
                    if (f == "Sent")
                        continue;
                    else if (f == folderName)
                        e.Add(new ContainerElement(f, "", "green"));
                    else
                        e.Add(new ButtonElementJS(f, null, $"Move('{HttpUtility.UrlEncode(f)}')"));
            } break;

            case "/move/do":
            { POST(req);
                if (InvalidMailboxOrMessageOrFolder(req, out var mailbox, out _, out var messageId, out var folder, out var folderName))
                    break;
                if ((!req.Query.TryGetValue("new", out var newFolderName)) || folderName == "Sent" || newFolderName == "Sent" || folderName == newFolderName)
                    throw new BadRequestSignal();
                if (!mailbox.Folders.TryGetValue(newFolderName, out var newFolder))
                    throw new NotFoundSignal();
                mailbox.Lock();
                folder.Remove(messageId);
                newFolder.Add(messageId);
                mailbox.UnlockSave();
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