#nullable disable
using NUnit.Framework;
using KitchenDesigner.Core.Update;

/// <summary>Разбор ответа GitHub /releases/latest. Все входные данные — строки-
/// фикстуры, сеть не трогается. Проверяется и успешный путь, и все ошибки.</summary>
public class ReleaseManifestParserTests
{
    private const string OneAsset = @"{
        ""tag_name"": ""v0.700"",
        ""name"": ""Kitchen Designer 0.700"",
        ""assets"": [
            { ""name"": ""KitchenDesigner-Setup-0.700-x64.exe"",
              ""browser_download_url"": ""https://gh/x/KitchenDesigner-Setup-0.700-x64.exe"" }
        ]
    }";

    [Test]
    public void Parse_Success_PicksSetupAsset()
    {
        Assert.IsTrue(ReleaseManifestParser.TryParse(OneAsset, out var m, out var err), err);
        Assert.AreEqual("0.700", m.Version);
        Assert.AreEqual("KitchenDesigner-Setup-0.700-x64.exe", m.FileName);
        Assert.AreEqual("https://gh/x/KitchenDesigner-Setup-0.700-x64.exe", m.DownloadUrl);
    }

    [Test]
    public void Parse_WhenVersionMatchesTag_PrefersThatAsset()
    {
        // Лишний leftover-артефакт предыдущей версии не должен побеждать.
        const string json = @"{
            ""tag_name"": ""v0.700"",
            ""assets"": [
                { ""name"": ""KitchenDesigner-Setup-0.699-x64.exe"", ""browser_download_url"": ""a"" },
                { ""name"": ""KitchenDesigner-Setup-0.700-x64.exe"", ""browser_download_url"": ""b"" }
            ]
        }";
        Assert.IsTrue(ReleaseManifestParser.TryParse(json, out var m, out _));
        Assert.AreEqual("b", m.DownloadUrl);
    }

    [Test]
    public void Parse_OnlyAssetIsFromAnotherVersion_Fails()
    {
        const string json = @"{
            ""tag_name"": ""v0.700"",
            ""assets"": [
                { ""name"": ""KitchenDesigner-Setup-0.662-x64.exe"", ""browser_download_url"": ""old"" }
            ]
        }";
        Assert.IsFalse(ReleaseManifestParser.TryParse(json, out _, out var err),
            "Ассет от чужой версии брать нельзя: publish-github.cmd умел догрузить старый setup " +
            "в новый релиз, и тогда апдейтер ставил бы 0.662 по тегу v0.700 — после перезапуска " +
            "версия остаётся старой и обновление предлагается снова, бесконечно.");
        Assert.That(err, Does.Contain("0.700"),
            "Ошибка должна называть версию, для которой установщика не нашлось.");
    }

    [Test]
    public void Parse_AssetOfALongerVersion_DoesNotPassForItsPrefix()
    {
        const string json = @"{
            ""tag_name"": ""v0.70"",
            ""assets"": [
                { ""name"": ""KitchenDesigner-Setup-0.700-x64.exe"", ""browser_download_url"": ""x"" }
            ]
        }";
        Assert.IsFalse(ReleaseManifestParser.TryParse(json, out _, out _),
            "«0.70» — префикс «0.700»; сравнение подстрокой без разделителей приняло бы чужой ассет.");
    }

    [Test]
    public void Parse_IgnoresNonSetupAssets()
    {
        const string json = @"{
            ""tag_name"": ""v0.700"",
            ""assets"": [
                { ""name"": ""icon.png"", ""browser_download_url"": ""x"" },
                { ""name"": ""KitchenDesigner-Setup-0.700-x64.zip"", ""browser_download_url"": ""y"" }
            ]
        }";
        Assert.IsFalse(ReleaseManifestParser.TryParse(json, out _, out var err));
        Assert.That(err, Does.Contain("x64"));
    }

    [Test]
    public void Parse_RejectsNonX64Setup()
    {
        const string json = @"{
            ""tag_name"": ""v0.700"",
            ""assets"": [
                { ""name"": ""KitchenDesigner-Setup-0.700-x86.exe"", ""browser_download_url"": ""x"" }
            ]
        }";
        Assert.IsFalse(ReleaseManifestParser.TryParse(json, out _, out _));
    }

    [Test]
    public void Parse_NoAssets_Fails()
    {
        const string json = @"{ ""tag_name"": ""v0.700"" }";
        Assert.IsFalse(ReleaseManifestParser.TryParse(json, out _, out var err));
        Assert.That(err, Does.Contain("x64"));
    }

    [Test]
    public void Parse_EmptyOrNullJson_Fails()
    {
        Assert.IsFalse(ReleaseManifestParser.TryParse(null, out _, out _));
        Assert.IsFalse(ReleaseManifestParser.TryParse("", out _, out _));
        Assert.IsFalse(ReleaseManifestParser.TryParse("   ", out _, out _));
    }

    [Test]
    public void Parse_MissingTag_Fails()
    {
        const string json = @"{ ""assets"": [
            { ""name"": ""KitchenDesigner-Setup-0.700-x64.exe"", ""browser_download_url"": ""x"" } ] }";
        Assert.IsFalse(ReleaseManifestParser.TryParse(json, out _, out var err));
        Assert.That(err, Is.Not.Empty);   // ошибка: в ответе нет номера версии
    }

    [Test]
    public void Parse_AssetWithEmptyUrl_IsSkipped()
    {
        const string json = @"{
            ""tag_name"": ""v0.700"",
            ""assets"": [
                { ""name"": ""KitchenDesigner-Setup-0.700-x64.exe"", ""browser_download_url"": """" }
            ]
        }";
        Assert.IsFalse(ReleaseManifestParser.TryParse(json, out _, out _));
    }
}
