namespace SelfControl.UiTests;

public class PlaceholderTests
{
    [Fact]
    public void AppInfo_HasName() => Assert.Equal("SelfControl", SelfControl.App.AppInfo.Name);
}
