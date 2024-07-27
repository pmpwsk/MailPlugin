namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public override async Task Handle(Request req)
    {
        switch (Parsers.GetFirstSegment(req.Path, out _))
        {
            case "forward":
                await HandleForward(req);
                break;
            case "manage":
                await HandleManage(req);
                break;
            case "move":
                await HandleMove(req);
                break;
            case "send":
                await HandleSend(req);
                break;
            case "settings":
                await HandleSettings(req);
                break;
            default:
                await HandleOther(req);
                break;
        }
    }
}