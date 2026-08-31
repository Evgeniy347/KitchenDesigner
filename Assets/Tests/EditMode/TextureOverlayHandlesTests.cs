using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ручки области накладки: коды рёбер, включение карандашом, порядок
/// «сначала снэп, потом ограничения» и меши наконечников.</summary>
public class TextureOverlayHandlesTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private static readonly Vector2Int Face = new Vector2Int(3000, 2500);

    private KitchenElement CreateWall()
    {
        var go = new GameObject("Стена");
        _spawned.Add(go);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var element = go.AddComponent<KitchenElement>();
        element.PartName = "Стена";
        element.DimensionsMM = new Vector3Int(3000, 2500, 100);
        go.AddComponent<Wall>();
        return element;
    }

    [TearDown]
    public void TearDown()
    {
        TextureOverlayHandles.End();
        TextureOverlayRenderer.ClearAll();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    private static RectInt Area() => new RectInt(500, 400, 800, 600);

    [Test]
    public void EdgeCodes_EachOneMovesItsOwnBoundary_AndLeavesTheOppositeAlone()
    {
        var r = Area();

        var minU = TextureOverlayHandles.StretchRect(r, TextureOverlayHandle.EdgeMinU, 600f, 0f, Face);
        Assert.AreEqual(600, minU.xMin, "ребро 0 — левое (−U)");
        Assert.AreEqual(r.xMax, minU.xMax, "противоположная граница стоит на месте");

        var maxU = TextureOverlayHandles.StretchRect(r, TextureOverlayHandle.EdgeMaxU, 1400f, 0f, Face);
        Assert.AreEqual(1400, maxU.xMax, "ребро 1 — правое (+U)");
        Assert.AreEqual(r.xMin, maxU.xMin);

        var minV = TextureOverlayHandles.StretchRect(r, TextureOverlayHandle.EdgeMinV, 0f, 500f, Face);
        Assert.AreEqual(500, minV.yMin, "ребро 2 — нижнее (−V)");
        Assert.AreEqual(r.yMax, minV.yMax);

        var maxV = TextureOverlayHandles.StretchRect(r, TextureOverlayHandle.EdgeMaxV, 0f, 1100f, Face);
        Assert.AreEqual(1100, maxV.yMax, "ребро 3 — верхнее (+V)");
        Assert.AreEqual(r.yMin, maxV.yMin);
    }

    [Test]
    public void EdgeCodes_TheTwoUEdgesMoveAlongU_AndTheTwoVEdgesAlongV()
    {
        var r = Area();

        foreach (int edge in new[] { TextureOverlayHandle.EdgeMinU, TextureOverlayHandle.EdgeMaxU })
        {
            var moved = TextureOverlayHandles.MoveRect(r, edge, 100f, 900f, Face);
            Assert.AreEqual(r.xMin + 100, moved.xMin, "рёбра 0 и 1 везут область вдоль U");
            Assert.AreEqual(r.yMin, moved.yMin, "поперечную координату перенос не трогает");
        }

        foreach (int edge in new[] { TextureOverlayHandle.EdgeMinV, TextureOverlayHandle.EdgeMaxV })
        {
            var moved = TextureOverlayHandles.MoveRect(r, edge, 900f, 100f, Face);
            Assert.AreEqual(r.yMin + 100, moved.yMin, "рёбра 2 и 3 везут область вдоль V");
            Assert.AreEqual(r.xMin, moved.xMin);
        }
    }

    [Test]
    public void StretchRect_CannotTurnTheAreaInsideOut_NorPushItOffTheFace()
    {
        var r = Area();
        int min = TextureOverlaySpec.MIN_SIZE_MM;

        var crossed = TextureOverlayHandles.StretchRect(r, TextureOverlayHandle.EdgeMinU, 5000f, 0f, Face);
        Assert.AreEqual(r.xMax - min, crossed.xMin,
            "тяга сквозь противоположную границу упирается в минимальный размер, "
            + "а не выворачивает область наизнанку");
        Assert.AreEqual(min, crossed.width);

        var offFace = TextureOverlayHandles.StretchRect(r, TextureOverlayHandle.EdgeMaxU, 9999f, 0f, Face);
        Assert.AreEqual(Face.x, offFace.xMax, "за край грани область не уходит");

        var negative = TextureOverlayHandles.StretchRect(r, TextureOverlayHandle.EdgeMinV, 0f, -500f, Face);
        Assert.AreEqual(0, negative.yMin, "и за нижний край тоже");
    }

    [Test]
    public void MoveRect_KeepsTheSize_AndStopsAtTheFaceEdge()
    {
        var r = Area();

        var far = TextureOverlayHandles.MoveRect(r, TextureOverlayHandle.EdgeMaxU, 9999f, 0f, Face);
        Assert.AreEqual(r.width, far.width, "перенос не меняет размер области");
        Assert.AreEqual(r.height, far.height);
        Assert.AreEqual(Face.x, far.xMax, "упёршаяся в край область просто останавливается");

        var back = TextureOverlayHandles.MoveRect(r, TextureOverlayHandle.EdgeMinV, 0f, -9999f, Face);
        Assert.AreEqual(0, back.yMin);
        Assert.AreEqual(r.height, back.height);
    }

    [Test]
    public void Stretch_SnapsTheGrabPoint_AndTheLimitsStillWin()
    {
        var r = Area();
        var neighbourEdges = new List<int> { 505 };

        Assert.IsTrue(TextureOverlaySnap.Nearest(neighbourEdges, 520f, 50f, out int snapped));
        var rect = TextureOverlayHandles.StretchRect(r, TextureOverlayHandle.EdgeMaxU, snapped, 0f, Face);

        Assert.AreEqual(r.xMin + TextureOverlaySpec.MIN_SIZE_MM, rect.xMax,
            "прилипает ТОЧКА ТЯГИ, а границы грани и минимальный размер накладывает "
            + "StretchRect уже после: порядок «сначала снэп, потом ограничения» "
            + "не даёт снэпу их обойти");
    }

    [Test]
    public void FullFaceOverlay_BecomesAnExplicitRectangle_OnTheFirstDrag()
    {
        var full = TextureOverlaySpec.FullFace(OverlaySide.A, "oak");
        Assume.That(full.IsFullFace, Is.True);

        var resolved = full.Resolve(Face);
        Assert.AreEqual(new RectInt(0, 0, Face.x, Face.y), resolved,
            "накладка «во всю грань» разворачивается в прямоугольник грани");

        var fixedRect = full.WithRect(resolved);
        Assert.IsFalse(fixedRect.IsFullFace,
            "после первого же перетаскивания область фиксируется в явных "
            + "миллиметрах, иначе тяга за край не имела бы от чего отсчитываться");
        Assert.AreEqual(Face.x, fixedRect.widthMM);
    }

    [Test]
    public void Toggle_OnTheSameRow_TurnsTheHandlesOff()
    {
        var wall = CreateWall();
        wall.SetTextureOverlays(new List<TextureOverlaySpec>
        {
            new TextureOverlaySpec(OverlaySide.A, "oak", 100, 100, 800, 600),
            new TextureOverlaySpec(OverlaySide.B, "white", 100, 100, 800, 600),
        });

        TextureOverlayHandles.Toggle(wall, 0);
        Assert.IsTrue(TextureOverlayHandles.IsEditing(wall, 0), "карандаш включил ручки");

        TextureOverlayHandles.Toggle(wall, 0);
        Assert.IsFalse(TextureOverlayHandles.Active,
            "повторный клик по карандашу той же строки — выключение");

        TextureOverlayHandles.Toggle(wall, 0);
        TextureOverlayHandles.Toggle(wall, 1);
        Assert.IsTrue(TextureOverlayHandles.IsEditing(wall, 1),
            "карандаш соседней строки переключает правку на неё, а не выключает");
    }

    [Test]
    public void Begin_OnTheAllSidesOverlay_RefusesToStart()
    {
        var wall = CreateWall();
        wall.SetTextureOverlays(new List<TextureOverlaySpec>
        {
            new TextureOverlaySpec(OverlaySide.All, "oak", 100, 100, 800, 600),
        });

        TextureOverlayHandles.Begin(wall, 0);

        Assert.IsFalse(TextureOverlayHandles.Active,
            "«(все)» — это шесть граней сразу, общей плоскости у них нет и тянуть "
            + "область не за что: сторону надо сперва выбрать конкретную");
    }

    [Test]
    public void PointerOverHandle_WhileNothingIsBeingEdited_IsFalse()
    {
        Assume.That(TextureOverlayHandles.Active, Is.False);

        Assert.IsFalse(TextureOverlayHandles.PointerOverHandle(),
            "пока карандаш не нажат, ручек нет — выделение, перетаскивание объекта "
            + "и панорама камеры не должны глохнуть на пустом месте");
    }

    [Test]
    public void ConeMesh_IsAUnitConeAlongPlusZ()
    {
        var cone = TextureOverlayHandles.ConeMesh();

        Assert.AreEqual(1f, cone.bounds.size.z, 1e-4f,
            "конус единичного масштаба: длину наконечника задаёт localScale ручки");
        Assert.AreEqual(0.5f, cone.bounds.max.z, 1e-4f, "вершина при z = +0.5");
        Assert.AreEqual(-0.5f, cone.bounds.min.z, 1e-4f, "основание при z = −0.5");
        Assert.AreEqual(0.5f, cone.bounds.max.x, 1e-4f, "радиус основания 0.5");
        Assert.AreEqual(1f, cone.bounds.size.y, 1e-4f);
    }

    [Test]
    public void CubeMesh_IsAUnitCubeBuiltByHand()
    {
        var cube = TextureOverlayHandles.CubeMesh();

        Assert.AreEqual(1f, cube.bounds.size.x, 1e-4f, "единичный куб");
        Assert.AreEqual(1f, cube.bounds.size.y, 1e-4f);
        Assert.AreEqual(1f, cube.bounds.size.z, 1e-4f);
        Assert.AreEqual(24, cube.vertexCount,
            "куб собран вручную по четыре вершины на грань: ручки накладки — "
            + "голые меши без коллайдера, поэтому им нечего терять на "
            + "WebGL-стриппинге (условие для примитивов держит "
            + "WebGLPrimitiveStrippingTests)");
        Assert.AreSame(cube, TextureOverlayHandles.CubeMesh(), "меш строится один раз");
    }
}
