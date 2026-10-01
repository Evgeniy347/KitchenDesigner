using KitchenDesigner.Editor;
using NUnit.Framework;

public class FontAssetSaveGuardTests
{
    private const string Font = "Assets/Resources/Fonts/LiberationSans SDF.asset";

    [Test]
    public void Filter_DropsTheDynamicFontAsset_KeepsEverythingElse()
    {
        var kept = FontAssetSaveGuard.Filter(new[] { "Assets/A.asset", Font, "Assets/B.unity" }, allowSave: false);

        CollectionAssert.AreEqual(new[] { "Assets/A.asset", "Assets/B.unity" }, kept,
            "динамический шрифт дописывает кернинг в asset при каждом прогоне (почти 3000 строк diff); "
            + "сохранение из рантайма и batch-прогонов должно его пропускать");
    }

    [Test]
    public void Filter_WithTheExplicitAllowance_KeepsTheFontAsset()
    {
        var kept = FontAssetSaveGuard.Filter(new[] { Font }, allowSave: true);

        CollectionAssert.AreEqual(new[] { Font }, kept,
            "пункт меню «Create TMP Font From LiberationSans» обязан уметь сохранить пересозданный шрифт");
    }

    [Test]
    public void GuardedPath_IsThePathTheCreatorWritesTo()
    {
        Assert.AreEqual(Font, CreateTMPFontFromLiberation.AssetPath,
            "сторож и создатель шрифта обязаны говорить об одном файле, иначе сторож охраняет пустоту");
    }
}
