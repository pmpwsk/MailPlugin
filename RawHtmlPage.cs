﻿namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    private class RawHtmlPage : IPage
    {
        private readonly string FilePath;

        public RawHtmlPage(string filePath)
        {
            FilePath = filePath;
        }

        public IEnumerable<string> Export(AppRequest _)
        {
            if (File.Exists(FilePath))
            {
                foreach (var line in File.ReadAllLines(FilePath))
                    yield return line;
            }
            else yield return "Not found!";
        }
    }
}