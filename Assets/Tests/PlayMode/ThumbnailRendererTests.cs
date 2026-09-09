using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

public class ThumbnailRendererTests
{
    private static readonly (string name, Func<GameObject> spawn)[] Kinds =
    {
        ("Board", () => ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Board", Vector3.zero)),
        ("Wall", () => ElementFactory.CreateWall(new Vector3Int(2000, 2500, 100), "Wall", Vector3.zero)),
        ("Facade", () => ElementFactory.CreateFacade(new Vector3Int(600, 720, 18), "Facade", Vector3.zero)),
        ("AssembledFacade", () => ElementFactory.CreateAssembledFacade(new Vector3Int(600, 720, 18),
            "AssembledFacade", Vector3.zero, AssembledFill.Blind)),
        ("Panel", () => ElementFactory.CreatePanel(new Vector3Int(600, 720, 18), "Panel", Vector3.zero)),
        ("RadialShelf", () => ElementFactory.CreateRadialShelf(600, 350, 18, 50, "RadialShelf", Vector3.zero)),
        ("Drawer", () => ElementFactory.CreateDrawer(DrawerType.B, 400, DrawerColor.White, 500,
            "Drawer", Vector3.zero)),
        ("Table", () => ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "Table", Vector3.zero)),
        ("RadiusTable", () => ElementFactory.CreateRadiusTable(new Vector3Int(1200, 750, 700),
            "RadiusTable", Vector3.zero)),
        ("Stool", () => ElementFactory.CreateStool(new Vector3Int(360, 450, 360), 20, "Stool", Vector3.zero)),
        ("Chair", () => ElementFactory.CreateChair(new Vector3Int(450, 900, 450), 20, 450,
            "Chair", Vector3.zero)),
        ("Sofa", () => ElementFactory.CreateSofa(new Vector3Int(1800, 850, 900), 30, 420,
            "Sofa", Vector3.zero)),
        ("Pouffe", () => ElementFactory.CreatePouffe(new Vector3Int(400, 400, 400), 20, 100,
            "Pouffe", Vector3.zero)),
        ("Toilet", () => ElementFactory.CreateToilet(400, "Toilet", Vector3.zero)),
        ("WallHungToilet", () => ElementFactory.CreateWallHungToilet(400, 1100,
            "WallHungToilet", Vector3.zero)),
        ("Bathtub", () => ElementFactory.CreateBathtub(
            new Vector3Int(BathtubElement.DefaultWidthMM, BathtubElement.DefaultHeightMM,
                BathtubElement.DefaultDepthMM),
            BathtubElement.DefaultRimWidthMM, BathtubElement.DefaultBowlDepthMM,
            BathtubElement.DefaultBowlRadiusMM, BathtubElement.DefaultBowlFilletMM,
            "Bathtub", Vector3.zero)),
        ("BathMixer", () => ElementFactory.CreateBathMixer(BathMixerSpec.Default, "BathMixer", Vector3.zero)),
        ("ShowerColumn", () => ElementFactory.CreateShowerColumn(ShowerColumnSpec.Default,
            "ShowerColumn", Vector3.zero)),
        ("Socket", () => ElementFactory.CreateSocket(WallDeviceSpec.Default, "Socket", Vector3.zero)),
        ("LightSwitch", () => ElementFactory.CreateLightSwitch(WallDeviceSpec.Default, true, null,
            "LightSwitch", Vector3.zero)),
        ("Bed", () => ElementFactory.CreateBed(new Vector3Int(1600, 500, 2000), true, true,
            "Bed", Vector3.zero)),
        ("Pillar", () => ElementFactory.CreatePillar(1200, "Pillar", Vector3.zero)),
        ("Floor", () => ElementFactory.CreateFloor(new Vector3Int(3000, 20, 3000), "Floor", Vector3.zero)),
        ("LightSource", () => ElementFactory.CreateLightSource("LightSource", Vector3.zero)),
        ("Sink", () => ElementFactory.CreateSink("Sink", Vector3.zero)),
        ("Cooktop", () => ElementFactory.CreateCooktop("Cooktop", Vector3.zero)),
        ("ScrewLeg", () => ElementFactory.CreateScrewLeg("ScrewLeg", Vector3.zero)),
        ("Pipe", () => ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, 500, "Pipe", Vector3.zero)),
        ("PipeElbow", () => ElementFactory.CreatePipeElbow("PipeElbow", Vector3.zero)),
        ("PipeCoupling", () => ElementFactory.CreatePipeCoupling("PipeCoupling", Vector3.zero)),
        ("PipeTee", () => ElementFactory.CreatePipeTee("PipeTee", Vector3.zero)),
        ("PipeCap", () => ElementFactory.CreatePipeCap("PipeCap", Vector3.zero)),
        ("PipeSupply", () => ElementFactory.CreatePipeSupply("PipeSupply", Vector3.zero)),
        ("PipeReturn", () => ElementFactory.CreatePipeReturn("PipeReturn", Vector3.zero)),
        ("Oven", () => ElementFactory.CreateOven("Oven", Vector3.zero)),
        ("Dishwasher", () => ElementFactory.CreateDishwasher("Dishwasher", Vector3.zero)),
        ("Window", () => ElementFactory.CreateWindow(new Vector3Int(1200, 1400, 150),
            "Window", Vector3.zero)),
        ("Door", () => ElementFactory.CreateDoor(new Vector3Int(900, 2000, 150), "Door", Vector3.zero)),
    };

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        PlayModeTestConfig.ConfigureForTests();
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        PartRegistry.Clear();
    }

    private static float NonBackgroundFraction(RenderTexture rt)
    {
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        var pixels = tex.GetPixels32();
        int painted = 0;
        foreach (var p in pixels)
            if (p.a > 8) painted++;

        UnityEngine.Object.DestroyImmediate(tex);
        return (float)painted / pixels.Length;
    }

    // Сенсор кадрирования: печатает по каждому типу габарит Renderer.bounds,
    // которым IsoCameraRig считает дистанцию, саму дистанцию и итоговую долю
    // закрашенных пикселей. Без этого «пусто/не пусто» ничего не говорит о
    // ПРИЧИНЕ — маленький Bounds, огромный Bounds или дистанция, которая не
    // зависит от Bounds вовсе (упёрлась в пол). Один список печатается всегда,
    // а не только при провале: сравнение винтовой опоры с соседями по этому
    // же логу — единственный способ увидеть, чем она отличается количественно.
    [UnityTest]
    public IEnumerator EveryKind_RendersNonEmptyTexture()
    {
        var empty = new List<string>();
        var log = new List<string>();
        foreach (var (name, spawn) in Kinds)
        {
            var rt = ThumbnailRenderer.Render(spawn, ThumbnailRenderer.DefaultSize, out var framing);
            float painted = NonBackgroundFraction(rt);
            log.Add(string.Format(
                "{0,-16} bounds={1} maxDim={2:F4} camDist={3:F4} painted={4:P2}",
                name, framing.Bounds.size, framing.MaxDimension, framing.CameraDistance, painted));
            if (painted < 0.01f) empty.Add(name + " (" + painted.ToString("P1") + ")");
            UnityEngine.Object.DestroyImmediate(rt);
            yield return null;
        }

        Debug.Log("ThumbnailRenderer framing sensor:\n" + string.Join("\n", log));

        Assert.IsEmpty(empty,
            "ThumbnailRenderer вернул пустую (прозрачную) текстуру для типов: "
            + string.Join(", ", empty)
            + ". Камера не видит меш — либо элемент не создался, либо изоляция по слою "
            + "срезала объект вместе с фоном.");
    }

    // Старая версия этого теста смотрела в реестр ПОСЛЕ DestroyImmediate и потому
    // доказывала только «мёртвой записи не осталось» — этого недостаточно: сам
    // KitchenElement.Awake() зовёт PartRegistry.Register синхронно при AddComponent,
    // ДО того как ThumbnailRenderer успевает что-либо решить, и тот же Register
    // дёргает SceneChangeTracker.NoteMembershipChanged/SceneRevision.Bump/
    // SceneVisibilityManager.Invalidate — то есть реальная сцена пересчитывает
    // выводимые связи на каждую миниатюру, даже если запись потом благополучно
    // уходит из реестра при уборке. Поэтому здесь два сильных утверждения:
    // счётчик ревизии сцены не сдвинулся ВООБЩЕ, и внутри самого спауна
    // (до какой бы то ни было уборки) реестр не вырос ни на одну запись.
    [UnityTest]
    public IEnumerator SandboxElement_NeverRegistersInPartRegistry_AndNeverBumpsSceneRevision()
    {
        PartRegistry.Clear();
        int countDuringSpawn = -1;
        int revisionBefore = SceneRevision.Version;

        var rt = ThumbnailRenderer.Render(() =>
        {
            var go = Kinds[0].spawn();
            countDuringSpawn = PartRegistry.GetAll().Count;
            return go;
        });

        Assert.AreEqual(0, countDuringSpawn,
            "внутри спауна миниатюры (ДО какой-либо уборки) реестр вырос — значит "
            + "KitchenElement.Awake зарегистрировал элемент, невзирая на песочницу, и "
            + "реальная сцена уже пересчитала SceneChangeTracker/SceneVisibilityManager "
            + "на объект, который через мгновение будет уничтожен");
        Assert.AreEqual(0, PartRegistry.GetAll().Count,
            "ThumbnailRenderer обязан спавнить элемент в песочнице — он не должен попасть в реестр сцены");
        Assert.AreEqual(revisionBefore, SceneRevision.Version,
            "рендер одной миниатюры поднял SceneRevision — тот же счётчик двигает "
            + "undo/автосохранение/отпечаток сцены, и рендер плитки не имеет права его трогать");

        UnityEngine.Object.DestroyImmediate(rt);
        yield return null;
    }

    [UnityTest]
    public IEnumerator EveryKind_SnapshotSavedToTestResults()
    {
        string dir = Path.Combine(Application.dataPath, "..", "test-results", "thumbnails");
        Directory.CreateDirectory(dir);

        foreach (var (name, spawn) in Kinds)
        {
            var rt = ThumbnailRenderer.Render(spawn);

            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            string path = Path.Combine(dir, "thumb-" + name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Assert.IsTrue(File.Exists(path), "Плитка не сохранена: " + path);

            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            yield return null;
        }
    }
}
