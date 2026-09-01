using System.Collections.Generic;
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

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement MakeBoard(string name, string materialId)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), name, Vector3.zero);
        _spawned.Add(go);
        var el = go.GetComponent<KitchenElement>();
        el.MaterialId = materialId;
        return el;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
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

    // --- Предзагрузка декоров сцены ---

    [Test]
    public void PrefetchScene_LoadsOnlyDecorsUsedInScene()
    {
        MakeBoard("PrefetchBoard", DecorId);
        Assert.AreEqual(TextureState.NotRequested, MaterialCatalog.Get(DecorId).textureState);
        var unused = MaterialCatalog.Get("yasen_navarra_h1250");

        TextureLibrary.PrefetchScene();

        Assert.AreEqual(TextureState.Loaded, MaterialCatalog.Get(DecorId).textureState,
            "декор, который стоит в сцене, обязан приехать до первого кадра");
        Assert.AreEqual(TextureState.NotRequested, unused.textureState,
            "остальные грузятся лениво, пачкой их тянуть незачем");
    }

    // --- Перечитывание папки ---

    [Test]
    public void Reload_KeepsElementDecor_AndReappliesTexture()
    {
        // Reload уничтожает прежние картинки. Если он не пере-наденет декоры,
        // рендереры останутся с материалами, ссылающимися в пустоту.
        var el = MakeBoard("ReloadBoard", MaterialCatalog.DefaultId);
        MaterialManager.ApplyById(el, DecorId);

        int count = TextureLibrary.Reload();

        Assert.Greater(count, 1, "индекс перечитан");
        Assert.AreEqual(DecorId, el.MaterialId, "элемент не забывает свой декор");
        var r = el.GetComponentInChildren<MeshRenderer>();
        Assert.IsNotNull(r.sharedMaterial.mainTexture, "картинка снова на материале");
        Assert.AreEqual(Color.white, r.sharedMaterial.GetColor("_BaseColor"),
            "после перезагрузки цвет-заглушка не должен вернуться поверх картинки");
    }

    [Test]
    public void Reload_ForgetsPreviouslyLoadedTextures()
    {
        var def = MaterialCatalog.Get(DecorId);
        TextureLibrary.Request(def);
        Assert.AreEqual(TextureState.Loaded, def.textureState);

        TextureLibrary.Reload();

        Assert.AreEqual(TextureState.NotRequested, MaterialCatalog.Get(DecorId).textureState,
            "каталог собран заново, картинки грузятся по новой");
    }

    [Test]
    public void Host_OutsidePlayMode_RefusesToStartACoroutine()
    {
        // Статике нужен MonoBehaviour, чтобы чего-то ждать, а в EditMode корутин
        // нет вовсе. Отказ обязан быть ЯВНЫМ: звавший по нему откатывает своё
        // состояние (снимает флаг «разбор очереди идёт»), иначе очередь картинок
        // встаёт навсегда и декоры так и остаются на цвете-заглушке.
        Assert.IsFalse(Application.isPlaying, "проверяем именно EditMode");
        Assert.IsFalse(TextureLibraryHost.Run(NothingToWaitFor()),
            "в EditMode корутину запускать негде — Run обязан вернуть ложь, "
            + "а не молча проглотить запуск");
    }

    private static System.Collections.IEnumerator NothingToWaitFor()
    {
        yield break;
    }
}
