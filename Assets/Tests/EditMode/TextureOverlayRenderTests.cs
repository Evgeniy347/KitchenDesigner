using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

public class TextureOverlayRenderTests
{
    private const int WideFaceE = 4;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement CreateWall(Vector3Int dims)
    {
        var go = new GameObject("Стена");
        _spawned.Add(go);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var element = go.AddComponent<KitchenElement>();
        element.PartName = "Стена";
        element.DimensionsMM = dims;
        go.AddComponent<Wall>();
        return element;
    }

    [TearDown]
    public void TearDown()
    {
        TextureOverlayRenderer.ClearAll();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    [Test]
    public void OverlaysOnOneFace_AreLiftedApartInListOrder_SoTheyDoNotFightForTheZBuffer()
    {
        Assert.Greater(TextureOverlayRenderer.LiftOfLayer(0), 0f,
            "даже первый слой приподнят над гранью, иначе он мерцает с самой стеной");
        Assert.Greater(TextureOverlayRenderer.LiftOfLayer(1), TextureOverlayRenderer.LiftOfLayer(0),
            "накладки на одной стороне идут стопкой: без разноса по высоте они мерцают");

        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[]
        {
            TextureOverlaySpec.FullFace(OverlaySide.E, "oak"),
            TextureOverlaySpec.FullFace(OverlaySide.E, "oak"),
        });

        var quads = TextureOverlayRenderer.QuadsOf(wall);
        Assert.AreEqual(2, quads.Count);

        var normal = wall.GetFaces()[WideFaceE].normal;
        float first = Vector3.Dot(quads[0].transform.position, normal);
        float second = Vector3.Dot(quads[1].transform.position, normal);
        Assert.Greater(second, first, "вторая накладка стопки лежит выше первой");
    }

    [Test]
    public void Quad_IsNotMirrored_ItsLocalXRunsAlongTheFaceRightAxis()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });

        var quad = TextureOverlayRenderer.QuadsOf(wall)[0];
        var face = wall.GetFaces()[WideFaceE];

        Assert.AreEqual(1f, Vector3.Dot(quad.transform.right, face.rightAxis), 1e-3f,
            "локальный +X накладки обязан идти вдоль face.rightAxis: при зеркальном повороте "
            + "направленный рисунок (грейн, плитка со швом) ложится наизнанку");
        Assert.AreEqual(1f, Vector3.Dot(quad.transform.forward, face.normal), 1e-3f);
    }

    [Test]
    public void Quad_CastsNoShadow_BecauseItIsAFilmOnTheSurfaceItCovers()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });

        var renderer = TextureOverlayRenderer.QuadsOf(wall)[0].GetComponent<MeshRenderer>();

        Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode,
            "собственная тень накладки легла бы на стену, которую накладка и покрывает");
        Assert.IsTrue(renderer.receiveShadows);
    }

    [Test]
    public void Root_StaysAtIdentity_SoAQuadLocalTransformIsAlreadyItsWorldOne()
    {
        var root = TextureOverlayRenderer.Root();

        Assert.IsNull(root.parent, "корень накладок висит в мире, а не под элементом");
        Assert.AreEqual(Vector3.zero, root.position);
        Assert.AreEqual(Vector3.one, root.localScale,
            "масштаб элемента не имеет права просочиться в накладку: у стены в localScale лежит "
            + "габарит, и накладка-ребёнок схлопнулась бы по толщине");
        Assert.AreEqual(Quaternion.identity.eulerAngles, root.rotation.eulerAngles);
    }

    [Test]
    public void OffsetFromFaceCentre_IsHalfTheShiftOfTheArea()
    {
        var faceMM = new Vector2Int(3000, 2500);

        var centred = TextureOverlayRenderer.OffsetFromFaceCentreMM(
            new RectInt(0, 0, 3000, 2500), faceMM);
        Assert.AreEqual(Vector2.zero, centred, "накладка во всю грань стоит по центру грани");

        var leftHalf = TextureOverlayRenderer.OffsetFromFaceCentreMM(
            new RectInt(0, 0, 1500, 2500), faceMM);
        Assert.AreEqual(-750f, leftHalf.x, 1e-3f,
            "меш центрирован на середине области: её центр уехал от центра грани на четверть ширины");
        Assert.AreEqual(0f, leftHalf.y, 1e-3f);
    }

    [Test]
    public void NarrowWallFace_TakesNoOpeningHoles()
    {
        var thinAlongZ = new Vector3Int(3000, 2500, 100);

        Assert.IsTrue(TextureOverlayRenderer.IsWideWallFace(thinAlongZ, WideFaceE),
            "проём режет ровно те две широкие грани, сквозь которые его прогоняет Wall.RebuildMesh");
        Assert.IsTrue(TextureOverlayRenderer.IsWideWallFace(thinAlongZ, WideFaceE + 1));
        for (int face = 0; face < 4; face++)
            Assert.IsFalse(TextureOverlayRenderer.IsWideWallFace(thinAlongZ, face),
                "на торце стены проёма нет, грань " + face);

        var thinAlongX = new Vector3Int(100, 2500, 3000);
        Assert.IsTrue(TextureOverlayRenderer.IsWideWallFace(thinAlongX, 0),
            "у стены, повёрнутой на 90°, широкими становятся другие две грани");
        Assert.IsFalse(TextureOverlayRenderer.IsWideWallFace(thinAlongX, WideFaceE));
    }

    [Test]
    public void HalfExtentOfAnOpening_IsProjectedOntoTheFaceAxis_NotGuessedFromWidth()
    {
        var go = new GameObject("Окно");
        _spawned.Add(go);
        var window = go.AddComponent<KitchenElement>();
        window.DimensionsMM = new Vector3Int(900, 1400, 100);

        Assert.AreEqual(450f, TextureOverlayRenderer.HalfExtentAlong(window, Vector3.right), 1e-2f,
            "невёрнутый проём: половина ширины по X");

        go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        Assert.AreEqual(50f, TextureOverlayRenderer.HalfExtentAlong(window, Vector3.right), 1e-2f,
            "после поворота вдоль мировой оси X лежит уже толщина: «ширина проёма ↔ ось грани» "
            + "угадывать нельзя, только проецировать");
        Assert.AreEqual(450f, TextureOverlayRenderer.HalfExtentAlong(window, Vector3.forward), 1e-2f);
        Assert.AreEqual(700f, TextureOverlayRenderer.HalfExtentAlong(window, Vector3.up), 1e-2f);
    }

    [Test]
    public void LoweredWall_ShowsNoOverlays_BecauseTheStubIsNotTheFullFace()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        var wallComp = wall.GetComponent<Wall>();
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });
        Assert.AreEqual(1, TextureOverlayRenderer.QuadsOf(wall).Count);

        wallComp.SetLowered(true, 0.4f);
        TextureOverlayRenderer.SyncAll();

        Assert.AreEqual(0, TextureOverlayRenderer.QuadsOf(wall).Count,
            "опущенная стена — временный обрубок: накладка по ПОЛНОЙ грани висела бы в воздухе");

        wallComp.RestoreFull();
        TextureOverlayRenderer.SyncAll();
        Assert.AreEqual(1, TextureOverlayRenderer.QuadsOf(wall).Count,
            "стена вернулась на полную высоту — накладка возвращается вместе с ней");
    }

    [Test]
    public void SyncAll_ForgetsTheOverlaysOfADestroyedElement()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });
        Assert.AreEqual(1, TextureOverlayRenderer.QuadCount);

        Object.DestroyImmediate(wall.gameObject);
        TextureOverlayRenderer.SyncAll();

        Assert.AreEqual(0, TextureOverlayRenderer.QuadCount,
            "осиротевшие накладки обязаны сноситься: иначе они остаются висеть в мире без хозяина");
    }
}
