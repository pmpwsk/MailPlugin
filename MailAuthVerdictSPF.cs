﻿namespace uwap.WebFramework.Plugins;

public partial class MailPlugin : Plugin
{
    public enum MailAuthVerdict
    {
        HardFail = -2,
        SoftFail = -1,
        Unset = 0,
        Pass = 1
    }
}