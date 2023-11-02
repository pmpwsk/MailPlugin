namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private class RawHtmlCodePage : IPage
    {
        private readonly string Code;

        public RawHtmlCodePage(string code)
        {
            Code = code;
        }

        public IEnumerable<string> Export(AppRequest _)
        {
            yield return Code;
        }
    }
}