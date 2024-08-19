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
			"/manifest.json" => System.Text.Encoding.UTF8.GetBytes($"{{\n  \"name\": \"Mail ({Parsers.DomainMain(domain)})\",\n  \"short_name\": \"Mail\",\n  \"start_url\": \"{pathPrefix}/\",\n  \"display\": \"minimal-ui\",\n  \"background_color\": \"#000000\",\n  \"theme_color\": \"#202024\",\n  \"orientation\": \"portrait-primary\",\n  \"icons\": [\n    {{\n      \"src\": \"{pathPrefix}/icon.svg\",\n      \"type\": \"image/svg+xml\",\n      \"sizes\": \"any\"\n    }},\n    {{\n      \"src\": \"{pathPrefix}/icon.png\",\n      \"type\": \"image/png\",\n      \"sizes\": \"512x512\"\n    }},\n    {{\n      \"src\": \"{pathPrefix}/icon.ico\",\n      \"type\": \"image/x-icon\",\n      \"sizes\": \"16x16 24x24 32x32 48x48 64x64 72x72 96x96 128x128 256x256\"\n    }}\n  ],\n  \"launch_handler\": {{\n    \"client_mode\": \"navigate-new\"\n  }},\n  \"related_applications\": [\n    {{\n      \"platform\": \"webapp\",\n      \"url\": \"{pathPrefix}/manifest.json\"\n    }}\n  ],\n  \"offline_enabled\": false,\n  \"omnibox\": {{\n    \"keyword\": \"mail\"\n  }},\n  \"version\": \"0.1.1\"\n}}\n"),
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
			"/forward.js" => "1724065743960",
			"/icon-red.ico" => "1724065743960",
			"/icon-red.png" => "1724065743960",
			"/icon-red.svg" => "1724065743960",
			"/icon.ico" => "1724065743960",
			"/icon.png" => "1724065743960",
			"/icon.svg" => "1724065743960",
			"/manage-mailbox.js" => "1724065743964",
			"/manage.js" => "1724065743964",
			"/manifest.json" => "1724065743964",
			"/message.js" => "1724065743964",
			"/move.js" => "1724065743964",
			"/query.js" => "1724065743964",
			"/send.js" => "1724065743964",
			"/settings.js" => "1724065743964",
			"/send/attachments.js" => "1724065743964",
			"/send/contacts.js" => "1724065743964",
			"/send/preview.js" => "1724081283687",
			"/settings/auth.js" => "1724065743964",
			"/settings/contacts-edit.js" => "1724065743964",
			"/settings/contacts.js" => "1724065743964",
			"/settings/folders.js" => "1724065743964",
			_ => null
		};
	
	private static readonly System.Resources.ResourceManager PluginFiles_ResourceManager = new("MailPlugin.Properties.PluginFiles", typeof(MailPlugin).Assembly);
}