using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Сборка короба 600×400 из четырёх деталей 18 мм с пазом под ДВП.
/// Проверяет, что задняя стенка реально садится в паз: заходит на глубину
/// меньше 7 мм (не упирается в дно) и больше нуля (не выпадает).
///
/// Геометрия повторяет тестовую сцену: box_2/box_3 — фронт и тыл (600×400×18),
/// box_1/box_4 — верх и низ (600×370×18), ДВП закрывает торец короба.</summary>
public class GrooveCarcassTests
{
    private const int BoardMM = 18;      // толщина детали короба
    private const int OuterW = 600;      // габарит короба по ширине
    private const int OuterH = 400;      // габарит короба по высоте
    private const int DepthMM = 370;     // расстояние между фронтом и тылом
    private const int DvpMM = 3;         // толщина ДВП (паз 4 мм минус 1 мм)

    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement Board(string name, Vector3Int dims)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = name;
        el.DimensionsMM = dims;
        return el;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    /// <summary>Насколько глубоко ДВП заходит в паз детали, мм.
    /// panelSpan — размер ДВП поперёк проёма, openingMM — чистый проём.</summary>
    private static float EngagementMM(int panelSpan, int openingMM)
        => (panelSpan - openingMM) / 2f;

    [Test]
    public void Carcass_FourBoards_AllCarryGrooveOnTheSameSide()
    {
        // Паз режется в пласти каждой детали вдоль кромки, к которой прижата ДВП.
        var boards = new[]
        {
            Board("box_1", new Vector3Int(OuterW, DepthMM, BoardMM)),
            Board("box_2", new Vector3Int(OuterW, OuterH, BoardMM)),
            Board("box_3", new Vector3Int(OuterW, OuterH, BoardMM)),
            Board("box_4", new Vector3Int(OuterW, DepthMM, BoardMM)),
        };

        foreach (var b in boards)
            Assert.IsTrue(b.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right)),
                $"{b.PartName}: сквозной паз у правой кромки");

        foreach (var b in boards)
        {
            Assert.AreEqual(1, b.Grooves.Count);
            var rect = GrooveMesh.ComputeRect(b.DimensionsMM, b.Grooves[0]);
            Assert.IsTrue(rect.IsValid, $"{b.PartName}: паз помещается на детали");

            // Паз стоит на 16 мм от правой кромки и имеет ширину 4 мм.
            float fromEdgeNear = (0.5f - rect.xMax) * b.DimensionsMM.x;
            float fromEdgeFar = (0.5f - rect.xMin) * b.DimensionsMM.x;
            Assert.AreEqual(AppConstants.GROOVE_OFFSET_MM, fromEdgeNear, 1e-2f,
                $"{b.PartName}: смещение паза от кромки");
            Assert.AreEqual(AppConstants.GROOVE_OFFSET_MM + AppConstants.GROOVE_WIDTH_MM,
                fromEdgeFar, 1e-2f, $"{b.PartName}: дальняя стенка паза");
        }
    }

    [Test]
    public void Dvp_IsThinnerThanGroove_SoItSlidesIn()
    {
        Assert.Less(DvpMM, AppConstants.GROOVE_WIDTH_MM,
            "ДВП должна быть тоньше паза — иначе не войдёт");
        Assert.AreEqual(1, AppConstants.GROOVE_WIDTH_MM - DvpMM,
            "технологический зазор по толщине — 1 мм");
    }

    [Test]
    public void Dvp_SeatsInsideGrooves_WithoutBottomingOut()
    {
        // Чистые проёмы короба, замеренные по сцене. По Y это ровно габарит минус
        // две детали; по Z — 371, а не 370: верх и низ (370 мм) не доходят до тыла
        // 1 мм, и этот зазор попадает в проём.
        const int openingZ = 371;
        int openingY = OuterH - 2 * BoardMM;     // 364: между верхом и низом
        Assert.AreEqual(364, openingY);

        // Размеры ДВП из тестовой сцены.
        const int dvpAlongZ = 383;
        const int dvpAlongY = 377;

        float engageZ = EngagementMM(dvpAlongZ, openingZ);
        float engageY = EngagementMM(dvpAlongY, openingY);

        foreach (var (axis, engage) in new[] { ("Z", engageZ), ("Y", engageY) })
        {
            Assert.Greater(engage, 0f, $"по {axis}: ДВП должна заходить в паз");
            Assert.Less(engage, AppConstants.GROOVE_DEPTH_MM,
                $"по {axis}: ДВП не должна упираться в дно паза (7 мм)");
        }

        // Фактические зазоры до дна паза — из размеров сцены.
        Assert.AreEqual(6.0f, engageZ, 1e-3f, "по Z заходит на 6 мм (зазор до дна 1 мм)");
        Assert.AreEqual(6.5f, engageY, 1e-3f, "по Y заходит на 6.5 мм (зазор до дна 0.5 мм)");
    }

    [Test]
    public void Dvp_SeatedPanel_IsReportedAsOverlapByValidator()
    {
        // Документирует известное ограничение: пазы не видны валидатору, поэтому
        // ПРАВИЛЬНО посаженная ДВП считается пересечением с деталью короба.
        var side = Board("box_2", new Vector3Int(OuterW, OuterH, BoardMM));
        side.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));
        side.transform.position = Vector3.zero;

        var dvp = Board("box_двп", new Vector3Int(383, 377, DvpMM));
        // Сажаем ДВП в паз: она заходит внутрь габарита детали короба.
        dvp.transform.position = new Vector3(0f, 0f, 0.006f);

        var result = ConstraintValidator.Validate(new List<KitchenElement> { side, dvp });

        Assert.IsTrue(result.violations.Contains(dvp) || result.violations.Contains(side),
            "пока паз не учитывается в габаритах, посадка в паз читается как overlap");
    }

    [Test]
    public void Grooves_AreInvisibleToSnapping_FacesStayOnBoundingBox()
    {
        // Прилипание работает по шести граням габарита; паз их не меняет,
        // поэтому «прилипнуть в паз» нельзя — ДВП сядет на пласть.
        var board = Board("box_2", new Vector3Int(OuterW, OuterH, BoardMM));
        var facesBefore = board.GetFaces();
        var vertsBefore = board.GetVertices();

        board.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Right));

        var facesAfter = board.GetFaces();
        var vertsAfter = board.GetVertices();

        Assert.AreEqual(6, facesAfter.Length, "граней по-прежнему шесть");
        for (int i = 0; i < facesBefore.Length; i++)
        {
            Assert.AreEqual(facesBefore[i].center, facesAfter[i].center, $"грань {i}: центр не сдвинулся");
            Assert.AreEqual(facesBefore[i].size, facesAfter[i].size, $"грань {i}: размер не изменился");
        }
        for (int i = 0; i < vertsBefore.Length; i++)
            Assert.AreEqual(vertsBefore[i], vertsAfter[i], $"вершина {i} не сдвинулась");
    }
}
