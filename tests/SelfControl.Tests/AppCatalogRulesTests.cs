using SelfControl.Core;

namespace SelfControl.Tests;

public class AppCatalogRulesTests
{
    [Fact]
    public void IconFile_StripsQuotesAndIndex() =>
        Assert.Equal(@"C:\Steam\steam.exe", AppCatalogRules.IconFile("\"C:\\Steam\\steam.exe\",0"));

    [Fact]
    public void PickExes_KeepsProgram_DropsHelpersAndProtected()
    {
        var exes = AppCatalogRules.PickExes(@"C:\Discord\app.ico", new[]
        {
            @"C:\Discord\Update.exe", @"C:\Discord\app-1.0\Discord.exe", @"C:\Discord\app-1.0\Discord.exe",
            @"C:\Discord\unins000.exe", @"C:\x\chrome.exe",
        });
        Assert.Equal(new[] { "discord.exe" }, exes);
    }

    [Fact]
    public void PickExes_UsesDisplayIconExe() =>
        Assert.Equal(new[] { "steam.exe" }, AppCatalogRules.PickExes("C:\\Steam\\steam.exe,0", Array.Empty<string>()));
}

public class AppCatalogBrowserTests
{
    [Fact]
    public void PickExes_Browser_IsSkippedEntirely() =>
        Assert.Empty(AppCatalogRules.PickExes(@"C:\Edge\msedge.exe,0", new[] { @"C:\Edge\msedge_proxy.exe", @"C:\Edge\cookie_exporter.exe" }));
}
