using KitchenServer.Web.Services;

namespace KitchenServer.Tests;

public class UnityWebGLStaticFilesTests
{
    [Theory]
    [InlineData("WebGL.wasm", "application/wasm", null)]
    [InlineData("WebGL.wasm.gz", "application/wasm", "gzip")]
    [InlineData("WebGL.wasm.br", "application/wasm", "br")]
    [InlineData("WebGL.framework.js", "application/javascript", null)]
    [InlineData("WebGL.framework.js.gz", "application/javascript", "gzip")]
    [InlineData("WebGL.data", "application/octet-stream", null)]
    [InlineData("WebGL.data.gz", "application/octet-stream", "gzip")]
    [InlineData("index.html", "text/html", null)]
    [InlineData("style.css", "text/css", null)]
    [InlineData("favicon.ico", "image/x-icon", null)]
    public void GetContentType_MapsUnityBuildFiles(string fileName, string expectedType, string? expectedEncoding)
    {
        var (contentType, encoding) = UnityWebGLStaticFiles.GetContentType(fileName);

        Assert.Equal(expectedType, contentType);
        Assert.Equal(expectedEncoding, encoding);
    }
}
