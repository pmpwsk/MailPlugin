﻿namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public enum MailAuthVerdict
    {
        Fail = -1,
        Unset = 0,
        Pass = 1
    }
}