using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

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
        TextureOverlayRenderer.ClearAll();
        EdgeSideHighlighter.Hide();
        EdgeSideHighlighter.MaterialFactory = null;
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

        var cmd = new SetTextureOverlaysCommand(wall, before, after);
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

        var json = JsonUtility.ToJson(ElementData.FromElement(wall));
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
        Assert.IsTrue(KitchenDesigner.Core.MCP.McpCommandHandler.TryParseTextureOverlays(
            "a:oak; f:white@100,200+800x600", out var parsed, out string error), error);

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual(TextureOverlaySpec.FullFace(OverlaySide.A, "oak"), parsed[0]);
        Assert.AreEqual(new TextureOverlaySpec(OverlaySide.F, "white", 100, 200, 800, 600), parsed[1]);
    }

    [Test]
    public void Mcp_EmptyStringClearsOverlays()
    {
        Assert.IsTrue(KitchenDesigner.Core.MCP.McpCommandHandler.TryParseTextureOverlays(
            "", out var parsed, out _));
        Assert.AreEqual(0, parsed.Count);
    }

    [Test]
    public void Mcp_RejectsUnknownSideAndBrokenArea()
    {
        Assert.IsFalse(KitchenDesigner.Core.MCP.McpCommandHandler.TryParseTextureOverlays(
            "z:oak", out _, out string sideError));
        StringAssert.Contains("unknown side", sideError);

        Assert.IsFalse(KitchenDesigner.Core.MCP.McpCommandHandler.TryParseTextureOverlays(
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

        string text = KitchenDesigner.Core.MCP.McpCommandHandler.FormatTextureOverlays(wall);
        Assert.IsTrue(KitchenDesigner.Core.MCP.McpCommandHandler.TryParseTextureOverlays(
            text, out var parsed, out string error), error);
        CollectionAssert.AreEqual(new List<TextureOverlaySpec>(wall.TextureOverlays), parsed);
    }

    // ── Подсветка грани ────────────────────────────────────────────────

    [Test]
    public void ShowFace_HighlightsExactlyOneFace_AtItsWorldSize()
    {
        var material = new Material(Shader.Find("Sprites/Default"));
        EdgeSideHighlighter.MaterialFactory = () => material;
        try
        {
            var wall = CreateWall(new Vector3Int(3000, 2500, 100));
            EdgeSideHighlighter.ShowFace(wall, 4);

            Assert.AreEqual(1, EdgeSideHighlighter.QuadCount,
                "подсвечивается ровно одна грань, без каёмок на соседних");
            Assert.IsTrue(EdgeSideHighlighter.IsFaceShown(wall, 4));

            var scale = EdgeSideHighlighter.QuadObjects[0].transform.lossyScale;
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
        EdgeSideHighlighter.MaterialFactory = () => material;
        try
        {
            var wall = CreateWall(new Vector3Int(3000, 2500, 100));
            EdgeSideHighlighter.ShowFace(wall, 0);
            EdgeSideHighlighter.Hide();
            Assert.AreEqual(0, EdgeSideHighlighter.QuadCount);
            Assert.IsFalse(EdgeSideHighlighter.IsFaceShown(wall, 0));
        }
        finally
        {
            Object.DestroyImmediate(material);
        }
    }
}
