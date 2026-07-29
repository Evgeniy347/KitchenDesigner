using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Tools;

/// <summary>Пипетка: разбор попадания в накладку текстуры, отмена смены декора
/// и взаимоисключение режимов-захватчиков мыши.</summary>
public class EyedropperTests
{
    // Стена 3000×2500×100 в начале координат: тонкая по Z, поэтому широкие грани
    // — индексы 4 (+Z) и 5 (−Z), их размер = (dims.x, dims.y), оси грани —
    // rightAxis = X, upAxis = Y.
    private static readonly Vector3Int WallDims = new Vector3Int(3000, 2500, 100);
    private const int FaceZPlus = 4;
    private static readonly Vector3 NormalZPlus = Vector3.forward;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement CreateWall()
    {
        var go = new GameObject("Стена");
        _spawned.Add(go);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var element = go.AddComponent<KitchenElement>();
        element.PartName = "Стена";
        element.DimensionsMM = WallDims;
        go.AddComponent<Wall>();
        return element;
    }

    private KitchenElement CreateBoard()
    {
        var go = new GameObject("Деталь");
        _spawned.Add(go);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        var element = go.AddComponent<KitchenElement>();
        element.PartName = "Деталь";
        element.DimensionsMM = new Vector3Int(800, 400, 18);
        return element;
    }

    /// <summary>Мировая точка на широкой грани стены по координатам грани (мм от
    /// её левого нижнего угла) — то же преобразование, что в TryFacePointMM, но
    /// в обратную сторону.</summary>
    private static Vector3 PointOnWall(int uMM, int vMM) => new Vector3(
        (uMM - WallDims.x * 0.5f) * AppConstants.MM_TO_UNITS,
        (vMM - WallDims.y * 0.5f) * AppConstants.MM_TO_UNITS,
        WallDims.z * 0.5f * AppConstants.MM_TO_UNITS);

    [SetUp]
    public void SetUp()
    {
        // Режимы и стек команд глобальны — тесты обязаны стартовать с чистого
        // состояния и возвращать его (см. AGENTS.md, снапшоты).
        EyedropperMode.Reset();
        KitchenDesigner.Core.Measure.MeasureMode.Reset();
        CommandStack.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        EyedropperMode.Reset();
        KitchenDesigner.Core.Measure.MeasureMode.Reset();
        CommandStack.Clear();
        TextureOverlayRenderer.ClearAll();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    // ── Разбор попадания в накладку ────────────────────────────────────

    [Test]
    public void TryPick_PointInsideOverlayArea_ReturnsItsMaterial()
    {
        var wall = CreateWall();
        wall.SetTextureOverlays(new[]
        {
            new TextureOverlaySpec(OverlaySide.E, "oak", 400, 300, 400, 400),
        });

        Assert.IsTrue(TextureOverlayPicker.TryPick(wall, PointOnWall(500, 400), NormalZPlus,
            out int index, out string id));
        Assert.AreEqual(0, index);
        Assert.AreEqual("oak", id);
    }

    [Test]
    public void TryPick_PointOutsideAllAreas_ReturnsFalse()
    {
        var wall = CreateWall();
        wall.SetTextureOverlays(new[]
        {
            new TextureOverlaySpec(OverlaySide.E, "oak", 400, 300, 400, 400),
        });

        Assert.IsFalse(TextureOverlayPicker.TryPick(wall, PointOnWall(2000, 2000), NormalZPlus,
            out _, out _), "мимо всех областей — красить и брать надо базовую «Текстуру»");
    }

    [Test]
    public void TryPick_OverlappingOverlays_LastInListWins()
    {
        var wall = CreateWall();
        // Обе накрывают точку (500, 400); вторая лежит выше — её и берём.
        wall.SetTextureOverlays(new[]
        {
            new TextureOverlaySpec(OverlaySide.E, "oak",   400, 300, 400, 400),
            new TextureOverlaySpec(OverlaySide.E, "wenge", 450, 350, 300, 300),
        });

        Assert.IsTrue(TextureOverlayPicker.TryPick(wall, PointOnWall(500, 400), NormalZPlus,
            out int index, out string id));
        Assert.AreEqual(1, index, "порядок в списке — это порядок слоёв");
        Assert.AreEqual("wenge", id);
    }

    [Test]
    public void TryPick_FullFaceOverlay_CoversWholeFace()
    {
        var wall = CreateWall();
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.E, "oak") });

        Assert.IsTrue(TextureOverlayPicker.TryPick(wall, PointOnWall(10, 10), NormalZPlus, out _, out string id));
        Assert.AreEqual("oak", id);
    }

    [Test]
    public void TryPick_OverlayOnAnotherSide_Ignored()
    {
        var wall = CreateWall();
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.A, "oak") });

        Assert.IsFalse(TextureOverlayPicker.TryPick(wall, PointOnWall(500, 400), NormalZPlus, out _, out _),
            "накладка на грани A не должна отзываться на клик по грани E");
    }

    [Test]
    public void TryPick_SideAll_MatchesAnyFace()
    {
        var wall = CreateWall();
        wall.SetTextureOverlays(new[] { TextureOverlaySpec.FullFace(OverlaySide.All, "oak") });

        Assert.IsTrue(TextureOverlayPicker.TryPick(wall, PointOnWall(500, 400), NormalZPlus, out _, out string id));
        Assert.AreEqual("oak", id);
    }

    [Test]
    public void TryPick_ElementWithoutOverlays_ReturnsFalse()
    {
        Assert.IsFalse(TextureOverlayPicker.TryPick(CreateBoard(), Vector3.zero, NormalZPlus, out _, out _));
    }

    [Test]
    public void NearestFaceIndex_FollowsHitNormal()
    {
        var wall = CreateWall();
        Assert.AreEqual(FaceZPlus, TextureOverlayPicker.NearestFaceIndex(wall, NormalZPlus));
        Assert.AreEqual(FaceZPlus + 1, TextureOverlayPicker.NearestFaceIndex(wall, -NormalZPlus));
        Assert.AreEqual(0, TextureOverlayPicker.NearestFaceIndex(wall, Vector3.right));
        Assert.AreEqual(2, TextureOverlayPicker.NearestFaceIndex(wall, Vector3.up));
    }

    [Test]
    public void TryFacePointMM_ConvertsCornerAndRejectsOutside()
    {
        var wall = CreateWall();

        Assert.IsTrue(TextureOverlayPicker.TryFacePointMM(wall, FaceZPlus, PointOnWall(0, 0), out Vector2Int corner));
        Assert.AreEqual(Vector2Int.zero, corner, "левый нижний угол грани — это (0, 0)");

        Assert.IsTrue(TextureOverlayPicker.TryFacePointMM(wall, FaceZPlus, PointOnWall(1200, 900), out Vector2Int uv));
        Assert.AreEqual(new Vector2Int(1200, 900), uv);

        Assert.IsFalse(TextureOverlayPicker.TryFacePointMM(wall, FaceZPlus, PointOnWall(-50, 900), out _),
            "точка за границей грани в накладку попасть не может");
    }

    // ── Отмена смены декора ────────────────────────────────────────────

    [Test]
    public void SetMaterialCommand_UndoRestoresPreviousDecor()
    {
        var board = CreateBoard();
        MaterialManager.ApplyById(board, "oak");

        CommandStack.Execute(new SetMaterialCommand(board, MaterialSlot.Base, "wenge"));
        Assert.AreEqual("wenge", board.MaterialId);

        CommandStack.Undo();
        Assert.AreEqual("oak", board.MaterialId, "Ctrl+Z обязан вернуть прежний декор");

        CommandStack.Redo();
        Assert.AreEqual("wenge", board.MaterialId);
    }

    [Test]
    public void SetMaterialCommand_ReadsBeforeFromElement()
    {
        var board = CreateBoard();
        MaterialManager.ApplyById(board, "concrete");

        CommandStack.Execute(new SetMaterialCommand(board, MaterialSlot.Base, "oak"));
        CommandStack.Undo();

        Assert.AreEqual("concrete", board.MaterialId);
    }

    [Test]
    public void MaterialIdOf_PlainBoard_AnySlotIsBaseDecor()
    {
        var board = CreateBoard();
        MaterialManager.ApplyById(board, "oak");

        Assert.AreEqual("oak", MaterialManager.MaterialIdOf(board, MaterialSlot.Base));
        Assert.AreEqual("oak", MaterialManager.MaterialIdOf(board, MaterialSlot.Tabletop),
            "у обычной детали столешницы нет — отвечает базовый декор");
        Assert.AreEqual("oak", MaterialManager.MaterialIdOf(board, MaterialSlot.Legs));
    }

    // ── Взаимоисключение режимов ───────────────────────────────────────

    [Test]
    public void Eyedropper_And_Measure_AreMutuallyExclusive()
    {
        KitchenDesigner.Core.Measure.MeasureMode.SetActive(true);
        EyedropperMode.SetActive(true);
        Assert.IsTrue(EyedropperMode.Active);
        Assert.IsFalse(KitchenDesigner.Core.Measure.MeasureMode.Active, "пипетка гасит рулетку");

        KitchenDesigner.Core.Measure.MeasureMode.SetActive(true);
        Assert.IsFalse(EyedropperMode.Active, "рулетка гасит пипетку");
        Assert.IsTrue(ToolMode.MouseCaptured);
    }

    [Test]
    public void MouseCaptured_OnlyWhileToolActive()
    {
        Assert.IsFalse(ToolMode.MouseCaptured);
        EyedropperMode.SetActive(true);
        Assert.IsTrue(ToolMode.MouseCaptured);
        EyedropperMode.SetActive(false);
        Assert.IsFalse(ToolMode.MouseCaptured);
    }

    [Test]
    public void PickedDecor_SurvivesLeavingTheMode()
    {
        EyedropperMode.SetActive(true);
        EyedropperMode.Pick("oak");
        EyedropperMode.SetActive(false);

        Assert.AreEqual("oak", EyedropperMode.PickedMaterialId,
            "вышли из режима подправить камеру — декор должен остаться в пипетке");
    }
}
