namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
	public override byte[]? GetFile(string relPath, string pathPrefix, string domain)
		=> relPath switch
		{
			"/forward.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File0"),
			"/icon-red.ico" => (byte[]?)PluginFiles_ResourceManager.GetObject("File1"),
			"/icon-red.png" => (byte[]?)PluginFiles_ResourceManager.GetObject("File2"),
			"/icon-red.svg" => (byte[]?)PluginFiles_ResourceManager.GetObject("File3"),
			"/icon.ico" => (byte[]?)PluginFiles_ResourceManager.GetObject("File4"),
			"/icon.png" => (byte[]?)PluginFiles_ResourceManager.GetObject("File5"),
			"/icon.svg" => (byte[]?)PluginFiles_ResourceManager.GetObject("File6"),
			"/manage-mailbox.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File7"),
			"/manage.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File8"),
			"/manifest.json" => System.Text.Encoding.UTF8.GetBytes($"{{\r\n  \"name\": \"Mail ({Parsers.DomainMain(domain)})\",\r\n  \"short_name\": \"Mail\",\r\n  \"start_url\": \"{pathPrefix}/\",\r\n  \"display\": \"minimal-ui\",\r\n  \"background_color\": \"#000000\",\r\n  \"theme_color\": \"#202024\",\r\n  \"orientation\": \"portrait-primary\",\r\n  \"icons\": [\r\n    {{\r\n      \"src\": \"{pathPrefix}/icon.svg\",\r\n      \"type\": \"image/svg+xml\",\r\n      \"sizes\": \"any\"\r\n    }},\r\n    {{\r\n      \"src\": \"{pathPrefix}/icon.png\",\r\n      \"type\": \"image/png\",\r\n      \"sizes\": \"512x512\"\r\n    }},\r\n    {{\r\n      \"src\": \"{pathPrefix}/icon.ico\",\r\n      \"type\": \"image/x-icon\",\r\n      \"sizes\": \"16x16 24x24 32x32 48x48 64x64 72x72 96x96 128x128 256x256\"\r\n    }}\r\n  ],\r\n  \"launch_handler\": {{\r\n    \"client_mode\": \"navigate-new\"\r\n  }},\r\n  \"related_applications\": [\r\n    {{\r\n      \"platform\": \"webapp\",\r\n      \"url\": \"{pathPrefix}/manifest.json\"\r\n    }}\r\n  ],\r\n  \"offline_enabled\": false,\r\n  \"omnibox\": {{\r\n    \"keyword\": \"mail\"\r\n  }},\r\n  \"version\": \"0.1.1\"\r\n}}\r\n"),
			"/message.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File9"),
			"/move.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File10"),
			"/query.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File11"),
			"/send.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File12"),
			"/settings.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File13"),
			"/send/attachments.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File14"),
			"/send/contacts.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File15"),
			"/send/preview.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File16"),
			"/settings/auth.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File17"),
			"/settings/contacts-edit.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File18"),
			"/settings/contacts.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File19"),
			"/settings/folders.js" => (byte[]?)PluginFiles_ResourceManager.GetObject("File20"),
			_ => null
		};
	
	public override string? GetFileVersion(string relPath)
		=> relPath switch
		{
			"/forward.js" => "1722120218549",
			"/icon-red.ico" => "1697666326000",
			"/icon-red.png" => "1697666261000",
			"/icon-red.svg" => "1697666263000",
			"/icon.ico" => "1694961189000",
			"/icon.png" => "1694960408000",
			"/icon.svg" => "1695860915000",
			"/manage-mailbox.js" => "1722119053568",
			"/manage.js" => "1722120060664",
			"/manifest.json" => "1722080362918",
			"/message.js" => "1722120136421",
			"/move.js" => "1722080921465",
			"/query.js" => "1722120227272",
			"/send.js" => "1732047810462",
			"/settings.js" => "1722114986859",
			"/send/attachments.js" => "1722034824924",
			"/send/contacts.js" => "1722034832111",
			"/send/preview.js" => "1724691200115",
			"/settings/auth.js" => "1722115373405",
			"/settings/contacts-edit.js" => "1722116471858",
			"/settings/contacts.js" => "1722116194459",
			"/settings/folders.js" => "1722120334453",
			_ => null
		};
	
	private static readonly System.Resources.ResourceManager PluginFiles_ResourceManager = new("MailPlugin.Properties.PluginFiles", typeof(MailPlugin).Assembly);
}