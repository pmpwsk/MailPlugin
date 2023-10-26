namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public readonly struct DomainSelectorPair
    {
        public readonly string Domain, Selector;

        public DomainSelectorPair(string domain, string selector)
        {
            Domain = domain;
            Selector = selector;
        }
    }
}