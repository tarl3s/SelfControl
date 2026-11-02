using Microsoft.Win32;
using SelfControl.Core;

namespace SelfControl.Desktop;

public sealed class RegistryAutostart : IAutostart
{
    const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "SelfControl";

    public bool Enabled
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(KeyPath);
            return k?.GetValue(ValueName) is string;
        }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(KeyPath);
            if (value) k.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --tray");
            else k.DeleteValue(ValueName, false);
        }
    }
}
