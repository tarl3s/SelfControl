using Microsoft.Toolkit.Uwp.Notifications;
using SelfControl.Core;

namespace SelfControl.Desktop;

public sealed class ToastNotifier : INotifier
{
    public void Show(string title, string message)
    {
        try
        {
            new ToastContentBuilder().AddText(string.IsNullOrEmpty(title) ? "SelfControl" : title).AddText(message).Show();
        }
        catch { /* notifications unavailable: never crash the app */ }
    }
}
