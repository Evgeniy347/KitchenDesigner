using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Настоящая загрузка картинки декора из папки текстур. Проверяет то,
/// что нельзя увидеть в чистых функциях: файл действительно читается, мипмапы
/// строятся, картинка сжимается, а отсутствующий файл не роняет приложение.</summary>
public class TextureLibraryTests
{
    // Квадрат 1080×1080 (стороны кратны 4 — значит сжатие обязано сработать).
    private const string DecorId = "dub_galifaks_belyy_h1176";

    [TearDown]
    public void Teardown()
    {
        MaterialManager.ClearCache();
        MaterialCatalog.Reset();
    }

    [Test]
    public void Folder_IsReachableAsFiles_InEditor()
    {
        Assert.IsTrue(TextureLibrary.HasFileAccess,
            $"в редакторе папка декоров должна читаться напрямую: {TextureLibrary.DirectoryPath}");
        Assert.IsTrue(File.Exists(TextureLibrary.IndexPath));
    }

    [Test]
    public void Request_ReadsRealImage_WithMipmaps()
    {
        var def = MaterialCatalog.Get(DecorId);
        Assert.AreEqual(DecorId, def.id, "декор должен быть в index.json");
        Assert.AreEqual(TextureState.NotRequested, def.textureState);

        TextureLibrary.Request(def);

        Assert.AreEqual(TextureState.Loaded, def.textureState);
        Assert.IsNotNull(def.texture);
        Assert.AreEqual(1080, def.texture!.width);
        Assert.Greater(def.texture.mipmapCount, 1,
            "без мипмапов мелкий рисунок на дальних деталях кипит");
        Assert.AreEqual(TextureWrapMode.Repeat, def.texture.wrapMode);
        Assert.AreEqual(FilterMode.Trilinear, def.texture.filterMode);
    }

    [Test]
    public void Request_CompressesTexture_WhenSidesAreMultiplesOfFour()
    {
        // Несжатая RGBA32 1080×1080 с мипами — 6,2 МБ против 0,8 МБ у DXT.
        var def = MaterialCatalog.Get(DecorId);
        TextureLibrary.Request(def);

        var format = def.texture!.format;
        Assert.That(format,
            Is.EqualTo(TextureFormat.DXT1).Or.EqualTo(TextureFormat.DXT5)
                .Or.EqualTo(TextureFormat.BC7),
            $"стороны кратны 4 — картинка обязана быть сжата, а не {format}");
    }

    [Test]
    public void Request_Twice_LoadsOnce()
    {
        var def = MaterialCatalog.Get(DecorId);
        TextureLibrary.Request(def);
        var first = def.texture;

        TextureLibrary.Request(def);

        Assert.AreSame(first, def.texture, "повторный запрос не должен перечитывать файл");
    }

    [Test]
    public void Request_MissingFile_MarksFailed_WithoutThrowing()
    {
        var def = new MaterialDef("no_such_decor", "Нет такого", "ЛДСП", Color.white,
            "no_such_file.png", 800);
        MaterialCatalog.Register(def);

        // Сбой пишется предупреждением, а не ошибкой: отсутствие одной картинки
        // не повод ронять весь каталог.
        Assert.DoesNotThrow(() => TextureLibrary.Request(def));

        Assert.AreEqual(TextureState.Failed, def.textureState);
        Assert.IsNull(def.texture);
    }

    [Test]
    public void Request_ColorOnlyDecor_DoesNothing()
    {
        var def = MaterialCatalog.Get("gtv_anthracite");
        Assert.IsFalse(def.HasTextureFile);

        TextureLibrary.Request(def);

        Assert.AreEqual(TextureState.NotRequested, def.textureState);
        Assert.IsNull(def.texture);
    }
}
