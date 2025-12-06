using System.Web;
using uwap.WebFramework.Elements;
using uwap.WebFramework.Responses;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public async Task<IResponse> HandleMove(Request req)
    {
        switch (req.Path)
        {
            // MOVE MAIL
            case "/move":
            { CreatePage(req, "Move", out var page, out var e);
                var (mailbox, messageId, message, folderName, _) = await ValidateMailboxAndMessageAndFolderAsync(req);
                if (folderName == "Sent")
                    return StatusResponse.BadRequest;
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
                return new LegacyPageResponse(page, req);
            }

            case "/move/do":
            { POST(req);
                var (mailbox, messageId, _, folderName, _) = await ValidateMailboxAndMessageAndFolderAsync(req);
                if (!req.Query.TryGetValue("new", out var newFolderName) || folderName == "Sent" || newFolderName == "Sent" || folderName == newFolderName)
                    return StatusResponse.BadRequest;
                
                await using var t = Mailboxes.StartModifying(ref mailbox);
                
                if (!mailbox.Folders.TryGetValue(folderName, out var folder)
                    || !mailbox.Folders.TryGetValue(newFolderName, out var newFolder))
                    return StatusResponse.NotFound;
                
                if (mailbox.Messages.TryGetValue(messageId, out var message))
                    if (newFolderName == "Trash")
                        message.Deleted = DateTime.UtcNow;
                    else message.Deleted = null;
                
                folder.Remove(messageId);
                newFolder.Add(messageId);
                return StatusResponse.Success;
            }
            



            // 404
            default:
                return StatusResponse.NotFound;
        }
    }
}