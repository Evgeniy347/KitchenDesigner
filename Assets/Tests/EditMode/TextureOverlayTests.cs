using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Накладки текстур: модель, геометрия области, меш с вырезами,
/// сохранение и MCP-контракт.</summary>
public class TextureOverlayTests
{
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

    private KitchenElement CreateBoard(Vector3Int dims)
    {
        var go = new GameObject("Деталь");
        _spawned.Add(go);
        var element = go.AddComponent<KitchenElement>();
        element.DimensionsMM = dims;
        return element;
    }

    [TearDown]
    public void TearDown()
    {
        TextureOverlayHandles.End();
        TextureOverlayRenderer.ClearAll();
        SideHighlighter.Hide();
        SideHighlighter.MaterialFactory = null;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    // ── Кто поддерживает накладки ──────────────────────────────────────

    [Test]
    public void Wall_SupportsTextureOverlays_PlainBoard_DoesNot()
    {
        Assert.IsTrue(CreateWall(new Vector3Int(3000, 2500, 100)).SupportsTextureOverlays);
        Assert.IsFalse(CreateBoard(new Vector3Int(800, 400, 18)).SupportsTextureOverlays,
            "у детали декор задаётся на весь щит, накладок у неё нет");
    }

    [Test]
    public void SetTextureOverlays_OnUnsupportedElement_IsIgnored()
    {
        var board = CreateBoard(new Vector3Int(800, 400, 18));
        board.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.A, "oak") });
        Assert.AreEqual(0, board.TextureOverlays.Count);
    }

    // ── Область накладки ───────────────────────────────────────────────

    [Test]
    public void NewOverlay_CoversWholeFace()
    {
        var spec = TextureOverlaySpec.FullFace(OverlaySide.E, "oak");
        Assert.IsTrue(spec.IsFullFace);

        var rect = spec.Resolve(new Vector2Int(3000, 2500));
        Assert.AreEqual(new RectInt(0, 0, 3000, 2500), rect);
    }

    [Test]
    public void FullFaceOverlay_FollowsResize()
    {
        var spec = TextureOverlaySpec.FullFace(OverlaySide.E, "oak");
        Assert.AreEqual(new RectInt(0, 0, 3000, 2500), spec.Resolve(new Vector2Int(3000, 2500)));
        // Габарит стены изменили — накладка «во всю грань» обязана растянуться
        // вместе с ней, а не остаться прямоугольником прежнего размера.
        Assert.AreEqual(new RectInt(0, 0, 4000, 2700), spec.Resolve(new Vector2Int(4000, 2700)));
    }

    [Test]
    public void ExplicitRect_IsClampedToFace()
    {
        var spec = new TextureOverlaySpec(OverlaySide.E, "oak", -200, 100, 5000, 600);
        Assert.AreEqual(new RectInt(0, 100, 3000, 600), spec.Resolve(new Vector2Int(3000, 2500)));
    }

    [Test]
    public void RectFullyOutsideFace_ResolvesEmpty()
    {
        var spec = new TextureOverlaySpec(OverlaySide.E, "oak", 4000, 100, 500, 600);
        var rect = spec.Resolve(new Vector2Int(3000, 2500));
        Assert.AreEqual(0, rect.width, "область целиком за гранью — рисовать нечего");
    }

    [Test]
    public void FaceSizeMM_MatchesFaceOrder()
    {
        var dims = new Vector3Int(3000, 2500, 100);
        // index/2 = ось, поперёк которой грань; оси грани — как в GetFaces.
        Assert.AreEqual(new Vector2Int(2500, 100), TextureOverlayGeometry.FaceSizeMM(dims, 0));
        Assert.AreEqual(new Vector2Int(3000, 100), TextureOverlayGeometry.FaceSizeMM(dims, 2));
        Assert.AreEqual(new Vector2Int(3000, 2500), TextureOverlayGeometry.FaceSizeMM(dims, 4));
    }

    [Test]
    public void AllSide_ExpandsToSixFaces()
    {
        Assert.AreEqual(6, TextureOverlayGeometry.FaceIndices(OverlaySide.All).Length);
        Assert.AreEqual(new[] { 3 }, TextureOverlayGeometry.FaceIndices(OverlaySide.D));
    }

    [Test]
    public void WithRect_TurnsFullFaceIntoExplicit()
    {
        var spec = TextureOverlaySpec.FullFace(OverlaySide.E, "oak");
        Assert.IsTrue(spec.IsFullFace);

        var rect = spec.WithRect(new RectInt(100, 200, 800, 600));
        Assert.IsFalse(rect.IsFullFace);
        Assert.AreEqual(100, rect.u0MM);
        Assert.AreEqual(200, rect.v0MM);
        Assert.AreEqual(800, rect.widthMM);
        Assert.AreEqual(600, rect.heightMM);
    }

    [Test]
    public void WithRect_ClampsToMinSize()
    {
        var spec = TextureOverlaySpec.FullFace(OverlaySide.E, "oak");
        var tiny = spec.WithRect(new RectInt(0, 0, 3, 5));
        Assert.AreEqual(TextureOverlaySpec.MIN_SIZE_MM, tiny.widthMM);
        Assert.AreEqual(TextureOverlaySpec.MIN_SIZE_MM, tiny.heightMM);
    }

    [Test]
    public void WithRect_PreservesMaterialAndSide()
    {
        var spec = new TextureOverlaySpec(OverlaySide.B, "white", 100, 200, 800, 600);
        var moved = spec.WithRect(new RectInt(50, 150, 900, 700));
        Assert.AreEqual(OverlaySide.B, moved.side);
        Assert.AreEqual("white", moved.MaterialId);
    }

    [Test]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new TextureOverlaySpec(OverlaySide.E, "oak", 100, 200, 800, 600);
        var b = new TextureOverlaySpec(OverlaySide.E, "oak", 100, 200, 800, 600);
        Assert.IsTrue(a.Equals(b));
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Test]
    public void Equals_DifferentSide_ReturnsFalse()
    {
        var a = new TextureOverlaySpec(OverlaySide.E, "oak", 100, 200, 800, 600);
        var b = new TextureOverlaySpec(OverlaySide.A, "oak", 100, 200, 800, 600);
        Assert.IsFalse(a.Equals(b));
    }

    [Test]
    public void Equals_DifferentMaterial_ReturnsFalse()
    {
        var a = new TextureOverlaySpec(OverlaySide.E, "oak", 100, 200, 800, 600);
        var b = new TextureOverlaySpec(OverlaySide.E, "white", 100, 200, 800, 600);
        Assert.IsFalse(a.Equals(b));
    }

    [Test]
    public void NewOverlay_DefaultMaterialIsUsedWhenNullOrEmpty()
    {
        var spec = new TextureOverlaySpec(OverlaySide.E, null, 100, 200, 800, 600);
        Assert.AreEqual(MaterialCatalog.DefaultId, spec.MaterialId);

        var spec2 = new TextureOverlaySpec(OverlaySide.E, "", 100, 200, 800, 600);
        Assert.AreEqual(MaterialCatalog.DefaultId, spec2.MaterialId);
    }

    // ── Ручки области ──────────────────────────────────────────────────

    [Test]
    public void Stretch_MovesOnlyGrabbedEdge()
    {
        var rect = new RectInt(100, 200, 800, 600);
        var face = new Vector2Int(3000, 2500);

        var right = TextureOverlayHandles.StretchRect(rect, 1, 1500f, 0f, face);
        Assert.AreEqual(new RectInt(100, 200, 1400, 600), right);

        var bottom = TextureOverlayHandles.StretchRect(rect, 2, 0f, 50f, face);
        Assert.AreEqual(new RectInt(100, 50, 800, 750), bottom);
    }

    [Test]
    public void Stretch_CannotTurnAreaInsideOut()
    {
        var rect = new RectInt(100, 200, 800, 600);
        var face = new Vector2Int(3000, 2500);

        // Тянем правую границу далеко ВЛЕВО за левую — область обязана остаться
        // не тоньше минимума, а не вывернуться наизнанку.
        var r = TextureOverlayHandles.StretchRect(rect, 1, -500f, 0f, face);
        Assert.AreEqual(100 + TextureOverlaySpec.MIN_SIZE_MM, r.xMax);
        Assert.AreEqual(TextureOverlaySpec.MIN_SIZE_MM, r.width);
    }

    [Test]
    public void Stretch_IsClampedToFace()
    {
        var r = TextureOverlayHandles.StretchRect(new RectInt(100, 200, 800, 600), 1,
            99999f, 0f, new Vector2Int(3000, 2500));
        Assert.AreEqual(3000, r.xMax);
    }

    [Test]
    public void Move_KeepsSize_AndStopsAtFaceEdge()
    {
        var rect = new RectInt(100, 200, 800, 600);
        var face = new Vector2Int(3000, 2500);

        var moved = TextureOverlayHandles.MoveRect(rect, 1, 300f, 999f, face);
        Assert.AreEqual(new RectInt(400, 200, 800, 600), moved,
            "ручка оси U везёт область только по U");

        var pushed = TextureOverlayHandles.MoveRect(rect, 1, 99999f, 0f, face);
        Assert.AreEqual(new RectInt(2200, 200, 800, 600), pushed, "упор в правый край грани");
        Assert.AreEqual(800, pushed.width, "перенос не меняет размер области");
    }

    [Test]
    public void Stretch_EachEdge_MovesOnlyThatEdge()
    {
        var rect = new RectInt(100, 200, 800, 600);
        var face = new Vector2Int(3000, 2500);

        var left = TextureOverlayHandles.StretchRect(rect, 0, 50f, 0f, face);
        Assert.AreEqual(50, left.xMin);
        Assert.AreEqual(900, left.xMax);

        var bottom = TextureOverlayHandles.StretchRect(rect, 2, 0f, 50f, face);
        Assert.AreEqual(50, bottom.yMin);
        Assert.AreEqual(800, bottom.yMax);

        var top = TextureOverlayHandles.StretchRect(rect, 3, 0f, 1500f, face);
        Assert.AreEqual(200, top.yMin);
        Assert.AreEqual(1500, top.yMax);
    }

    [Test]
    public void Move_IsAxisConstrained()
    {
        var rect = new RectInt(100, 200, 800, 600);
        var face = new Vector2Int(3000, 2500);

        var alongV = TextureOverlayHandles.MoveRect(rect, 3, 999f, 500f, face);
        Assert.AreEqual(new RectInt(100, 700, 800, 600), alongV);
    }

    [Test]
    public void Move_NegativeDelta_StopsAtZero()
    {
        var rect = new RectInt(100, 200, 800, 600);
        var face = new Vector2Int(3000, 2500);

        var left = TextureOverlayHandles.MoveRect(rect, 0, -9999f, 0f, face);
        Assert.AreEqual(0, left.xMin);

        var down = TextureOverlayHandles.MoveRect(rect, 2, 0f, -9999f, face);
        Assert.AreEqual(0, down.yMin);
    }

    [Test]
    public void Move_SizeWiderThanFace_StaysAtZero()
    {
        var rect = new RectInt(0, 0, 4000, 600);
        var face = new Vector2Int(3000, 2500);
        var moved = TextureOverlayHandles.MoveRect(rect, 1, 9999f, 0f, face);
        Assert.AreEqual(0, moved.xMin, "некуда двигать — и так на нуле");
    }

    // ── Жизненный цикл ручек ────────────────────────────────────────────

    [Test]
    public void Begin_NullElement_StaysInactive()
    {
        TextureOverlayHandles.Begin(null!, 0);
        Assert.IsFalse(TextureOverlayHandles.Active);
    }

    [Test]
    public void Begin_NegativeIndex_StaysInactive()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });
        TextureOverlayHandles.Begin(wall, -1);
        Assert.IsFalse(TextureOverlayHandles.Active);
    }

    [Test]
    public void Begin_IndexOutOfRange_StaysInactive()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        TextureOverlayHandles.Begin(wall, 0);
        Assert.IsFalse(TextureOverlayHandles.Active);
    }

    [Test]
    public void Begin_AllSide_IsRejected()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.All, "oak") });
        TextureOverlayHandles.Begin(wall, 0);
        Assert.IsFalse(TextureOverlayHandles.Active);
    }

    [Test]
    public void Begin_ThenEnd_ClearsActive()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });
        TextureOverlayHandles.Begin(wall, 0);
        Assert.IsTrue(TextureOverlayHandles.Active);
        TextureOverlayHandles.End();
        Assert.IsFalse(TextureOverlayHandles.Active);
    }

    [Test]
    public void Toggle_ActivatesThenDeactivates()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });
        TextureOverlayHandles.Toggle(wall, 0);
        Assert.IsTrue(TextureOverlayHandles.Active);
        TextureOverlayHandles.Toggle(wall, 0);
        Assert.IsFalse(TextureOverlayHandles.Active);
    }

    [Test]
    public void IsEditing_SameElementAndIndex_ReturnsTrue()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });
        TextureOverlayHandles.Begin(wall, 0);
        Assert.IsTrue(TextureOverlayHandles.IsEditing(wall, 0));
        TextureOverlayHandles.End();
    }

    [Test]
    public void IsEditing_DifferentElement_ReturnsFalse()
    {
        var wallA = CreateWall(new Vector3Int(3000, 2500, 100));
        var wallB = CreateWall(new Vector3Int(2000, 2500, 100));
        wallA.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });
        TextureOverlayHandles.Begin(wallA, 0);
        Assert.IsFalse(TextureOverlayHandles.IsEditing(wallB, 0));
        TextureOverlayHandles.End();
    }

    [Test]
    public void IsEditing_WrongIndex_ReturnsFalse()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[]
        {
            TextureOverlaySpec.FullFace(OverlaySide.E, "oak"),
            TextureOverlaySpec.FullFace(OverlaySide.A, "white"),
        });
        TextureOverlayHandles.Begin(wall, 0);
        Assert.IsFalse(TextureOverlayHandles.IsEditing(wall, 1));
        TextureOverlayHandles.End();
    }

    [Test]
    public void Begin_EachFaceSide_Activates()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 200));
        var sides = new[] { OverlaySide.A, OverlaySide.B, OverlaySide.C,
            OverlaySide.D, OverlaySide.E, OverlaySide.F };
        for (int i = 0; i < sides.Length; i++)
        {
            wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(sides[i], "oak") });
            TextureOverlayHandles.Begin(wall, 0);
            Assert.IsTrue(TextureOverlayHandles.Active, $"сторона {sides[i]} должна активировать ручки");
            TextureOverlayHandles.End();
        }
    }

    [Test]
    public void End_WhileInactive_IsSafe()
    {
        TextureOverlayHandles.End();
        Assert.IsFalse(TextureOverlayHandles.Active);
    }

    /// <summary>Две накладки на грани A: соседка занимает 1000…1800 по U.</summary>
    private static List<TextureOverlaySpec> TwoOnFaceA() => new List<TextureOverlaySpec>
    {
        new TextureOverlaySpec(OverlaySide.A, "oak", 100, 200, 800, 600),
        new TextureOverlaySpec(OverlaySide.A, "white", 1000, 400, 800, 500),
    };

    [Test]
    public void NeighbourEdges_TakesOnlyOtherOverlaysOfTheSameFace()
    {
        var face = new Vector2Int(3000, 2500);
        var overlays = TwoOnFaceA();
        overlays.Add(new TextureOverlaySpec(OverlaySide.B, "oak", 0, 0, 500, 500));

        var u = TextureOverlaySnap.NeighbourEdges(overlays, 0, (int)OverlaySide.A, face, alongU: true);
        CollectionAssert.AreEquivalent(new[] { 1000, 1800 }, u,
            "своя область и накладка чужой грани в кандидаты не идут");

        var v = TextureOverlaySnap.NeighbourEdges(overlays, 0, (int)OverlaySide.A, face, alongU: false);
        CollectionAssert.AreEquivalent(new[] { 400, 900 }, v);
    }

    [Test]
    public void NeighbourEdges_IncludeAllSideOverlay()
    {
        var face = new Vector2Int(3000, 2500);
        var overlays = new List<TextureOverlaySpec>
        {
            new TextureOverlaySpec(OverlaySide.A, "oak", 100, 200, 800, 600),
            new TextureOverlaySpec(OverlaySide.All, "white", 200, 0, 700, 500),
        };

        var u = TextureOverlaySnap.NeighbourEdges(overlays, 0, (int)OverlaySide.A, face, alongU: true);
        CollectionAssert.AreEquivalent(new[] { 200, 900 }, u,
            "«(все)» лежит и на этой грани — прилипать к ней можно");
    }

    [Test]
    public void Stretch_SnapsGrabbedEdgeToNeighbour()
    {
        var face = new Vector2Int(3000, 2500);
        var edges = TextureOverlaySnap.NeighbourEdges(TwoOnFaceA(), 0, (int)OverlaySide.A, face, true);

        // Тянем правую границу почти до левого края соседки (1000).
        Assert.IsTrue(TextureOverlaySnap.Nearest(edges, 985f, 50f, out int snapped));
        Assert.AreEqual(1000, snapped);
        var rect = TextureOverlayHandles.StretchRect(
            new RectInt(100, 200, 800, 600), 1, snapped, 0f, face);
        Assert.AreEqual(1000, rect.xMax, "область встала встык к соседке");
    }

    [Test]
    public void Snap_IgnoresEdgesBeyondThreshold_AndWhenDisabled()
    {
        var face = new Vector2Int(3000, 2500);
        var edges = TextureOverlaySnap.NeighbourEdges(TwoOnFaceA(), 0, (int)OverlaySide.A, face, true);

        Assert.IsFalse(TextureOverlaySnap.Nearest(edges, 900f, 50f, out _),
            "100 мм до соседки при пороге 50 — не прилипаем");
        Assert.IsFalse(TextureOverlaySnap.Nearest(edges, 999f, 0f, out _),
            "нулевой порог = привязка выключена");
        Assert.IsTrue(TextureOverlaySnap.Nearest(edges, 950f, 50f, out int onBorder),
            "ровно на пороге снэп срабатывает (как в ResizeSnap)");
        Assert.AreEqual(1000, onBorder);
    }

    [Test]
    public void Snap_PicksNearestEdge()
    {
        var edges = new List<int> { 1000, 1030 };
        Assert.IsTrue(TextureOverlaySnap.Nearest(edges, 1020f, 50f, out int snapped));
        Assert.AreEqual(1030, snapped);
    }

    [Test]
    public void Move_SnapsWholeAreaByEitherEdge_AndKeepsSize()
    {
        var face = new Vector2Int(3000, 2500);
        var edges = TextureOverlaySnap.NeighbourEdges(TwoOnFaceA(), 0, (int)OverlaySide.A, face, true);

        // Область 800 мм подъехала правым краем к 985 — прилипает к 1000.
        var moved = TextureOverlaySnap.SnapMoved(new RectInt(185, 200, 800, 600), true, edges, 50f, face);
        Assert.AreEqual(new RectInt(200, 200, 800, 600), moved);
        Assert.AreEqual(800, moved.width, "перенос со снэпом не меняет размер");

        // Левым краем к правому краю соседки (1800).
        var right = TextureOverlaySnap.SnapMoved(new RectInt(1780, 200, 800, 600), true, edges, 50f, face);
        Assert.AreEqual(1800, right.xMin);
    }

    [Test]
    public void Move_Snap_DoesNotPushAreaOffTheFace()
    {
        var face = new Vector2Int(2000, 2500);
        var edges = new List<int> { 1210 };

        // Прилипание левым краем к 1210 увело бы правый край за грань (2010).
        var moved = TextureOverlaySnap.SnapMoved(new RectInt(1180, 0, 800, 600), true, edges, 50f, face);
        Assert.AreEqual(1200, moved.xMin, "упор в край грани сильнее снэпа");
        Assert.AreEqual(2000, moved.xMax);
    }

    // ── Меш накладки ───────────────────────────────────────────────────

    [Test]
    public void Mesh_WithoutHoles_IsSingleQuad()
    {
        var mesh = PlaneWithHolesMesh.Build(new RectInt(0, 0, 3000, 2500), null,
            new Vector2Int(800, 800));
        Assert.IsNotNull(mesh);
        Assert.AreEqual(4, mesh!.vertexCount);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Mesh_UVsFollowPhysicalTile_NotAreaSize()
    {
        // Плитка 800 мм, область 1600 мм — ровно две плитки по ширине.
        var mesh = PlaneWithHolesMesh.Build(new RectInt(0, 0, 1600, 800), null,
            new Vector2Int(800, 800));
        Assert.IsNotNull(mesh);
        var uv = mesh!.uv;
        float maxU = 0f;
        foreach (var p in uv) maxU = Mathf.Max(maxU, p.x);
        Assert.AreEqual(2f, maxU, 1e-4f, "картинка повторяется, а не растягивается на область");
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Mesh_UVsAnchoredToFaceOrigin_NotToArea()
    {
        // Сдвинутая область показывает ДРУГОЙ кусок того же неподвижного рисунка:
        // на этом и держится «растяжение = изменение области отображения».
        var mesh = PlaneWithHolesMesh.Build(new RectInt(800, 0, 800, 800), null,
            new Vector2Int(800, 800));
        Assert.IsNotNull(mesh);
        float minU = float.MaxValue;
        foreach (var p in mesh!.uv) minU = Mathf.Min(minU, p.x);
        Assert.AreEqual(1f, minU, 1e-4f);
        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Mesh_HasHoleUnderOpening()
    {
        var hole = new RectInt(1000, 800, 900, 1400);
        var mesh = PlaneWithHolesMesh.Build(new RectInt(0, 0, 3000, 2500),
            new List<RectInt> { hole }, new Vector2Int(800, 800));
        Assert.IsNotNull(mesh);

        // Ни одна ячейка не покрывает центр проёма.
        float cx = (hole.xMin + hole.xMax) * 0.5f;
        float cy = (hole.yMin + hole.yMax) * 0.5f;
        Assert.IsFalse(CoversPoint(mesh!, new RectInt(0, 0, 3000, 2500), cx, cy),
            "под проёмом накладки быть не должно");
        // А точка рядом с проёмом — покрыта.
        Assert.IsTrue(CoversPoint(mesh!, new RectInt(0, 0, 3000, 2500), 200f, 200f));
        Object.DestroyImmediate(mesh);
    }

    /// <summary>Есть ли треугольник, накрывающий точку (мм в координатах грани).
    /// Меш центрирован на середине области и лежит в юнитах — пересчитываем.</summary>
    private static bool CoversPoint(Mesh mesh, RectInt rect, float xMM, float yMM)
    {
        float localX = (xMM - (rect.xMin + rect.xMax) * 0.5f) * AppConstants.MM_TO_UNITS;
        float localY = (yMM - (rect.yMin + rect.yMax) * 0.5f) * AppConstants.MM_TO_UNITS;
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            var a = verts[tris[i]];
            var b = verts[tris[i + 1]];
            var c = verts[tris[i + 2]];
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            if (localX > minX && localX < maxX && localY > minY && localY < maxY) return true;
        }
        return false;
    }

    // ── Рендер ─────────────────────────────────────────────────────────

    [Test]
    public void Renderer_BuildsOneQuadPerOverlay_WithWorldSizeOfTheArea()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });

        var quads = TextureOverlayRenderer.QuadsOf(wall);
        Assert.AreEqual(1, quads.Count);

        // Накладка НЕ должна унаследовать масштаб элемента: у стены в localScale
        // лежит габарит, и ребёнок схлопнулся бы по толщине.
        var bounds = quads[0].GetComponent<MeshFilter>().sharedMesh.bounds;
        Assert.AreEqual(3000f * AppConstants.MM_TO_UNITS, bounds.size.x, 1e-3f);
        Assert.AreEqual(2500f * AppConstants.MM_TO_UNITS, bounds.size.y, 1e-3f);
        Assert.AreEqual(Vector3.one, quads[0].transform.localScale);
    }

    [Test]
    public void Renderer_AllSide_BuildsSixQuads()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.All, "oak") });
        Assert.AreEqual(6, TextureOverlayRenderer.QuadsOf(wall).Count);
    }

    [Test]
    public void Renderer_CutsOpeningsOutOfOverlay()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        var wallComp = wall.GetComponent<Wall>();

        var winGo = new GameObject("Окно");
        _spawned.Add(winGo);
        winGo.AddComponent<MeshFilter>();
        winGo.AddComponent<MeshRenderer>();
        var window = winGo.AddComponent<WindowElement>();
        window.DimensionsMM = new Vector3Int(900, 1400, 100);
        winGo.transform.position = wall.transform.position;
        wallComp.RegisterWindow(window);

        // Грань E (индекс 4) — широкая сторона стены, сквозь неё и идёт проём.
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });

        var quads = TextureOverlayRenderer.QuadsOf(wall);
        Assert.AreEqual(1, quads.Count);
        Assert.Greater(quads[0].GetComponent<MeshFilter>().sharedMesh.vertexCount, 4,
            "проём режет накладку на ячейки — сплошного квада быть не может");
    }

    /// <summary>«Прозрачный» обязан гасить и накладки: непрозрачный меш поверх
    /// сквозной грани делал стену сплошной, и выключатель переставал работать.</summary>
    [Test]
    public void Renderer_HidesQuadsWhileElementIsTransparent()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });
        Assert.AreEqual(1, TextureOverlayRenderer.QuadsOf(wall).Count);

        wall.Transparent = true;
        TextureOverlayRenderer.SyncAll();
        Assert.AreEqual(0, TextureOverlayRenderer.QuadsOf(wall).Count);

        wall.Transparent = false;
        TextureOverlayRenderer.SyncAll();
        Assert.AreEqual(1, TextureOverlayRenderer.QuadsOf(wall).Count,
            "снятая прозрачность возвращает накладку");
    }

    [Test]
    public void Renderer_ClearsQuadsWhenOverlaysRemoved()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });
        Assert.AreEqual(1, TextureOverlayRenderer.QuadsOf(wall).Count);

        wall.SetTextureOverlays(new TextureOverlaySpec[0]);
        Assert.AreEqual(0, TextureOverlayRenderer.QuadsOf(wall).Count);
    }

    /// <summary>Стену можно вернуть из прозрачной обратно. Раньше ElementHighlighter
    /// выходил на стене досрочно: прозрачный материал ей никто не снимал, а подсветка
    /// выделения запоминала его как «исходный» и тащила дальше.</summary>
    [Test]
    public void WallTransparency_CanBeTurnedBackOff()
    {
        // Смена материала через renderer.material в EditMode заставляет Unity
        // ругаться на Destroy прежнего экземпляра. Это шум редактора, а не
        // проверяемая логика: в игре тот же код молчит.
        UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
        try
        {
            var hlGo = new GameObject("Highlighter");
            _spawned.Add(hlGo);
            var highlighter = hlGo.AddComponent<ElementHighlighter>();
            highlighter.RefreshHighlights(); // здесь же создаются материалы

            var wall = CreateWall(new Vector3Int(3000, 2500, 100));
            var renderer = wall.GetComponent<MeshRenderer>();

            wall.Transparent = true;
            highlighter.ApplyForElement(wall);
            Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent,
                renderer.sharedMaterial.renderQueue, "стена стала прозрачной");

            wall.Transparent = false;
            highlighter.ApplyForElement(wall);
            Assert.AreNotEqual((int)UnityEngine.Rendering.RenderQueue.Transparent,
                renderer.sharedMaterial.renderQueue, "и вернулась обратно");
        }
        finally
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }
    }

    // ── Undo ───────────────────────────────────────────────────────────

    [Test]
    public void SetTextureOverlaysCommand_UndoRestoresPreviousSet()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        var before = new List<TextureOverlaySpec>(wall.TextureOverlays);
        var after = new List<TextureOverlaySpec>
        {
            TextureOverlaySpec.FullFace(OverlaySide.E, "oak"),
        };

        var cmd = new SetListCommand<TextureOverlaySpec>(
            "Textures test", before, after, wall.SetTextureOverlays);
        cmd.Execute();
        Assert.AreEqual(1, wall.TextureOverlays.Count);

        cmd.Undo();
        Assert.AreEqual(0, wall.TextureOverlays.Count);
        Assert.AreEqual(0, TextureOverlayRenderer.QuadsOf(wall).Count);
    }

    // ── Сохранение ─────────────────────────────────────────────────────

    [Test]
    public void SaveRoundTrip_KeepsOverlays()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[]
        {
            TextureOverlaySpec.FullFace(OverlaySide.E, "oak"),
            new TextureOverlaySpec(OverlaySide.F, "white", 100, 200, 800, 600),
        });

        var json = JsonUtility.ToJson(ElementCapture.FromElement(wall));
        var restored = JsonUtility.FromJson<ElementData>(json).TextureOverlaySpecs();

        Assert.AreEqual(2, restored.Count);
        Assert.AreEqual(wall.TextureOverlays[0], restored[0]);
        Assert.AreEqual(wall.TextureOverlays[1], restored[1]);
    }

    [Test]
    public void OldSaveWithoutOverlays_LoadsAsEmpty()
    {
        var data = JsonUtility.FromJson<ElementData>("{\"name\":\"Стена\"}");
        Assert.AreEqual(0, data.TextureOverlaySpecs().Count);
    }

    // ── MCP ────────────────────────────────────────────────────────────

    [Test]
    public void Mcp_ParsesSideAndOptionalArea()
    {
        Assert.IsTrue(KitchenDesigner.Core.MCP.McpSpecCodec.TryParseTextureOverlays(
            "a:oak; f:white@100,200+800x600", out var parsed, out string error), error);

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual(TextureOverlaySpec.FullFace(OverlaySide.A, "oak"), parsed[0]);
        Assert.AreEqual(new TextureOverlaySpec(OverlaySide.F, "white", 100, 200, 800, 600), parsed[1]);
    }

    [Test]
    public void Mcp_EmptyStringClearsOverlays()
    {
        Assert.IsTrue(KitchenDesigner.Core.MCP.McpSpecCodec.TryParseTextureOverlays(
            "", out var parsed, out _));
        Assert.AreEqual(0, parsed.Count);
    }

    [Test]
    public void Mcp_RejectsUnknownSideAndBrokenArea()
    {
        Assert.IsFalse(KitchenDesigner.Core.MCP.McpSpecCodec.TryParseTextureOverlays(
            "z:oak", out _, out string sideError));
        StringAssert.Contains("unknown side", sideError);

        Assert.IsFalse(KitchenDesigner.Core.MCP.McpSpecCodec.TryParseTextureOverlays(
            "a:oak@100-200", out _, out string rectError));
        StringAssert.Contains("area", rectError);
    }

    [Test]
    public void Mcp_FormatIsParsedBack()
    {
        var wall = CreateWall(new Vector3Int(3000, 2500, 100));
        wall.SetTextureOverlays(new[]
        {
            TextureOverlaySpec.FullFace(OverlaySide.All, "oak"),
            new TextureOverlaySpec(OverlaySide.B, "white", 100, 200, 800, 600),
        });

        string text = KitchenDesigner.Core.MCP.McpSpecCodec.FormatTextureOverlays(wall);
        Assert.IsTrue(KitchenDesigner.Core.MCP.McpSpecCodec.TryParseTextureOverlays(
            text, out var parsed, out string error), error);
        CollectionAssert.AreEqual(new List<TextureOverlaySpec>(wall.TextureOverlays), parsed);
    }

    // ── Подсветка грани ────────────────────────────────────────────────

    [Test]
    public void ShowFace_HighlightsExactlyOneFace_AtItsWorldSize()
    {
        var material = new Material(Shader.Find("Sprites/Default"));
        SideHighlighter.MaterialFactory = () => material;
        try
        {
            var wall = CreateWall(new Vector3Int(3000, 2500, 100));
            SideHighlighter.ShowFace(wall, 4);

            Assert.AreEqual(1, SideHighlighter.QuadCount,
                "подсвечивается ровно одна грань, без каёмок на соседних");
            Assert.IsTrue(SideHighlighter.IsFaceShown(wall, 4));

            var scale = SideHighlighter.QuadObjects[0].transform.lossyScale;
            Assert.AreEqual(3000f * AppConstants.MM_TO_UNITS, scale.x, 1e-3f);
            Assert.AreEqual(2500f * AppConstants.MM_TO_UNITS, scale.y, 1e-3f);
        }
        finally
        {
            Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void ShowFace_ThenHide_RemovesHighlight()
    {
        var material = new Material(Shader.Find("Sprites/Default"));
        SideHighlighter.MaterialFactory = () => material;
        try
        {
            var wall = CreateWall(new Vector3Int(3000, 2500, 100));
            SideHighlighter.ShowFace(wall, 0);
            SideHighlighter.Hide();
            Assert.AreEqual(0, SideHighlighter.QuadCount);
            Assert.IsFalse(SideHighlighter.IsFaceShown(wall, 0));
        }
        finally
        {
            Object.DestroyImmediate(material);
        }
    }

    // ── DropdownHover: деактивация списка (не уничтожение) ─────────────

    private static readonly MethodInfo _dropdownHoverUpdate =
        typeof(DropdownHover).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>Наведение вешается компонентом, который умеет ТОЛЬКО вход и
    /// выход курсора. EventTrigger здесь запрещён: он реализует все интерфейсы
    /// событий сразу, забирает себе и колесо мыши, и прокрутка длинного списка
    /// перестаёт работать.</summary>
    [Test]
    public void DropdownHover_ItemHover_ReportsIndex_WithoutEatingScroll()
    {
        var root = new GameObject("TestRoot");
        try
        {
            var dd = UIFactory.CreateDropdown("TestDD", root.transform,
                new List<string> { "A", "B" }, Vector2.zero, new Vector2(200, 28), _ => { });
            int entered = -1;
            int exits = 0;
            DropdownHover.Attach(dd, i => entered = i, () => exits++);

            var listGo = new GameObject("Dropdown List");
            listGo.transform.SetParent(dd.transform, worldPositionStays: false);
            var second = MakeItem(listGo.transform, "Item 1");
            var first = MakeItem(listGo.transform, "Item 0");
            first.transform.SetSiblingIndex(0);
            _dropdownHoverUpdate!.Invoke(dd.GetComponent<DropdownHover>(), null);

            // Обработчиков наведения на пункте двое: сам Toggle (подсветка) и
            // наш — дёргаем оба, как это делает EventSystem.
            var evt = new UnityEngine.EventSystems.PointerEventData(null);
            foreach (var h in second.GetComponents<UnityEngine.EventSystems.IPointerEnterHandler>())
                h.OnPointerEnter(evt);
            Assert.AreEqual(1, entered, "наведение должно сообщать индекс пункта");

            foreach (var h in second.GetComponents<UnityEngine.EventSystems.IPointerExitHandler>())
                h.OnPointerExit(evt);
            Assert.AreEqual(1, exits, "уход курсора должен снимать предпросмотр");

            Assert.IsNull(second.GetComponent<UnityEngine.EventSystems.IScrollHandler>(),
                "BUG: пункт перехватывает колесо мыши — список не прокрутить");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject MakeItem(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);
        go.AddComponent<UnityEngine.UI.Toggle>();
        return go;
    }

    [Test]
    public void DropdownHover_ExitFires_WhenListIsDeactivated()
    {
        var root = new GameObject("TestRoot");
        try
        {
            var dd = UIFactory.CreateDropdown("TestDD", root.transform,
                new List<string> { "A", "B", "C" },
                Vector2.zero, new Vector2(200, 28), _ => { });
            int exitCalls = 0;
            DropdownHover.Attach(dd, _ => { }, () => exitCalls++);

            // Эмулируем открытие: список появился активным
            var listGo = new GameObject("Dropdown List");
            listGo.transform.SetParent(dd.transform, worldPositionStays: false);
            listGo.SetActive(true);
            _dropdownHoverUpdate!.Invoke(
                dd.GetComponent<DropdownHover>(), null);
            Assert.AreEqual(0, exitCalls, "до закрытия выхода быть не должно");

            // Эмулируем закрытие: список деактивирован (не уничтожен)
            listGo.SetActive(false);
            _dropdownHoverUpdate!.Invoke(
                dd.GetComponent<DropdownHover>(), null);
            Assert.AreEqual(1, exitCalls,
                "onExit должен сработать при деактивации списка — без" +
                " него подсветка остаётся висеть после выбора грани");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
    [Test]
    public void DropdownHover_InactiveTemplateItem_DoesNotShiftIndices()
    {
        var root = new GameObject("TestRoot");
        try
        {
            var dd = UIFactory.CreateDropdown("TestDD", root.transform,
                new List<string> { "A", "B" }, Vector2.zero, new Vector2(200, 28), _ => { });
            int entered = -1;
            DropdownHover.Attach(dd, i => entered = i, () => { });

            var listGo = new GameObject("Dropdown List");
            listGo.transform.SetParent(dd.transform, worldPositionStays: false);
            var template = MakeItem(listGo.transform, "Item");
            template.SetActive(false);
            MakeItem(listGo.transform, "Item 0");
            var second = MakeItem(listGo.transform, "Item 1");
            _dropdownHoverUpdate!.Invoke(dd.GetComponent<DropdownHover>(), null);

            var evt = new UnityEngine.EventSystems.PointerEventData(null);
            foreach (var h in second.GetComponents<UnityEngine.EventSystems.IPointerEnterHandler>())
                h.OnPointerEnter(evt);

            Assert.AreEqual(1, entered,
                "шаблонный пункт списка выключен и в нумерацию опций не входит — "
                + "иначе наведение показывает предпросмотр соседнего декора");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
