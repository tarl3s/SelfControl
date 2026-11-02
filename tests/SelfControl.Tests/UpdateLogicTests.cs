using SelfControl.Core;

namespace SelfControl.Tests;

public class UpdateLogicTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData(" V2.0.1 ", "2.0.1")]
    public void ParseVersion_ReadsTags(string tag, string expected) =>
        Assert.Equal(expected, UpdateLogic.ParseVersion(tag)!.ToString());

    [Theory]
    [InlineData("")] [InlineData("latest")] [InlineData(null)]
    public void ParseVersion_RejectsJunk(string? tag) => Assert.Null(UpdateLogic.ParseVersion(tag));

    [Fact]
    public void IsNewer_Compares()
    {
        Assert.True(UpdateLogic.IsNewer("v1.0.1", "1.0.0"));
        Assert.True(UpdateLogic.IsNewer("1.10.0", "1.9.0"));
        Assert.False(UpdateLogic.IsNewer("1.0.0", "1.0.0"));
        Assert.False(UpdateLogic.IsNewer("0.9.0", "1.0.0"));
        Assert.False(UpdateLogic.IsNewer("junk", "1.0.0"));
    }

    [Fact]
    public void ParseLatest_FindsZipAndNotes()
    {
        var json = """{"tag_name":"v1.1.0","body":"Fixes","assets":[{"name":"x.txt","browser_download_url":"u0"},{"name":"SelfControl-win-x64.zip","browser_download_url":"https://h/z.zip"}]}""";
        var r = UpdateLogic.ParseLatest(json)!;
        Assert.Equal("1.1.0", r.Version); Assert.Equal("Fixes", r.Notes); Assert.Equal("https://h/z.zip", r.ZipUrl);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"tag_name":"v1.1.0","assets":[]}""")]
    [InlineData("""{"tag_name":"beta","assets":[{"name":"SelfControl-win-x64.zip","browser_download_url":"u"}]}""")]
    public void ParseLatest_NullWhenUnusable(string json) => Assert.Null(UpdateLogic.ParseLatest(json));
}
