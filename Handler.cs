using uwap.WebFramework.Responses;

namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public override Task<IResponse> HandleAsync(Request req)
        => Parsers.GetFirstSegment(req.Path, out _) switch
        {
            "forward" => HandleForward(req),
            "manage" => HandleManage(req),
            "move" => HandleMove(req),
            "send" => HandleSend(req),
            "settings" => HandleSettings(req),
            _ => HandleOther(req)
        };
}