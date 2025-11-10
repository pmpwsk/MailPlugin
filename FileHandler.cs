namespace uwap.WebFramework.Plugins;

public partial class MailPlugin
{
    public override byte[]? GetFile(string relPath, string pathPrefix, string domain)
        => relPath switch
        {
            "/forward.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File0"),
            "/icon-red.ico" => (byte[]?)PackedFiles_ResourceManager.GetObject("File1"),
            "/icon-red.png" => (byte[]?)PackedFiles_ResourceManager.GetObject("File2"),
            "/icon-red.svg" => (byte[]?)PackedFiles_ResourceManager.GetObject("File3"),
            "/icon.ico" => (byte[]?)PackedFiles_ResourceManager.GetObject("File4"),
            "/icon.png" => (byte[]?)PackedFiles_ResourceManager.GetObject("File5"),
            "/icon.svg" => (byte[]?)PackedFiles_ResourceManager.GetObject("File6"),
            "/manage-mailbox.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File7"),
            "/manage.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File8"),
            "/manifest.json" => System.Text.Encoding.UTF8.GetBytes($"{{\r\n  \"name\": \"Mail ({Parsers.DomainMain(domain)})\",\r\n  \"short_name\": \"Mail\",\r\n  \"start_url\": \"{pathPrefix}/\",\r\n  \"display\": \"minimal-ui\",\r\n  \"background_color\": \"#000000\",\r\n  \"theme_color\": \"#202024\",\r\n  \"orientation\": \"portrait-primary\",\r\n  \"icons\": [\r\n    {{\r\n      \"src\": \"{pathPrefix}/icon.svg\",\r\n      \"type\": \"image/svg+xml\",\r\n      \"sizes\": \"any\"\r\n    }},\r\n    {{\r\n      \"src\": \"{pathPrefix}/icon.png\",\r\n      \"type\": \"image/png\",\r\n      \"sizes\": \"512x512\"\r\n    }},\r\n    {{\r\n      \"src\": \"{pathPrefix}/icon.ico\",\r\n      \"type\": \"image/x-icon\",\r\n      \"sizes\": \"16x16 24x24 32x32 48x48 64x64 72x72 96x96 128x128 256x256\"\r\n    }}\r\n  ],\r\n  \"launch_handler\": {{\r\n    \"client_mode\": \"navigate-new\"\r\n  }},\r\n  \"related_applications\": [\r\n    {{\r\n      \"platform\": \"webapp\",\r\n      \"url\": \"{pathPrefix}/manifest.json\"\r\n    }}\r\n  ],\r\n  \"offline_enabled\": false,\r\n  \"omnibox\": {{\r\n    \"keyword\": \"mail\"\r\n  }},\r\n  \"version\": \"0.1.1\"\r\n}}\r\n"),
            "/message.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File9"),
            "/move.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File10"),
            "/query.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File11"),
            "/send.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File12"),
            "/send/attachments.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File13"),
            "/send/contacts.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File14"),
            "/send/preview.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File15"),
            "/settings.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File16"),
            "/settings/auth.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File17"),
            "/settings/contacts-edit.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File18"),
            "/settings/contacts.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File19"),
            "/settings/folders.js" => (byte[]?)PackedFiles_ResourceManager.GetObject("File20"),
            _ => null
        };
    
    public override string? GetFileVersion(string relPath)
        => relPath switch
        {
            "/forward.js" => "638577170185485615",
            "/icon-red.ico" => "638332631260000000",
            "/icon-red.png" => "638332630610000000",
            "/icon-red.svg" => "638332630630000000",
            "/icon.ico" => "638305579890000000",
            "/icon.png" => "638305572080000000",
            "/icon.svg" => "638314577150000000",
            "/manage-mailbox.js" => "638577158535682286",
            "/manage.js" => "638577168606638271",
            "/manifest.json" => "638576771629180795",
            "/message.js" => "638577169364211666",
            "/move.js" => "638576777214654648",
            "/query.js" => "638577170272719722",
            "/send.js" => "638968457112370718",
            "/send/attachments.js" => "638576316249240646",
            "/send/contacts.js" => "638576316321107836",
            "/send/preview.js" => "638861964208869642",
            "/settings.js" => "638577117868587651",
            "/settings/auth.js" => "638577121734052379",
            "/settings/contacts-edit.js" => "638577132718581618",
            "/settings/contacts.js" => "638577129944590898",
            "/settings/folders.js" => "638577171344529208",
            _ => null
        };
    
    private static readonly System.Resources.ResourceManager PackedFiles_ResourceManager = new("MailPlugin.Properties.PackedFiles", typeof(MailPlugin).Assembly);
}
