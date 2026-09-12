using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Появление детали из сайдбара — такой же КОНЕЦ ЖЕСТА, как отпускание
/// мыши, и кончается тем же выравниванием по мм-сетке. Фабрика при этом ничего не
/// выравнивает и не должна: её зовёт не только кнопка, но и загрузка файла, а
/// загрузка данные пользователя не правит (<c>MillimetreGridFindingTests</c>).
/// Поэтому <c>MmGrid.Snap</c> стоит в <c>ElementCreation.Commit</c> — единственном
/// месте, где создание ложится в историю отмены, и стоит ДО записи: сдвиг и
/// создание отменяются ОДНИМ шагом, а не двумя.
///
/// На целое ставится КРАЙ — минимальный угол, а не центр. Причина не в сетке, а в
/// том, что человек читает раскрой и спецификацию по ГРАНЯМ: размер, зазор до
/// соседа, отступ от стены — всё это разности граней, и они целые ровно тогда,
/// когда целые сами грани. Центр 268,5 у доски шириной 537 — норма; правило
/// «центр на целом» дало бы в раскрое 537,5 и 283,75.
///
/// Три входа выбраны по трём разным источникам дробности, и ни один не заменяет
/// другой: НЕЧЁТНЫЙ ГАБАРИТ, который задаёт человек (доска 537), НЕЧЁТНАЯ
/// ПРОИЗВОДСТВЕННАЯ КОНСТАНТА (пятак опоры Ø25) и ДРОБНАЯ (фасад духовки 19,5 мм,
/// из-за которого коробка валидации стоит на четверти миллиметра — округляется
/// именно она, по GetVertices, а не голый трансформ: в спецификацию идёт эта
/// коробка).</summary>
public class ElementCreationGridTests
{
    private const float U = AppConstants.MM_TO_UNITS;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    private GameObject Created(GameObject go)
    {
        _spawned.Add(go);
        ElementCreation.Commit(go);
        return go;
    }

    private static List<AnalysisIssue> Grd01() =>
        SceneAnalyzer.Analyze()
            .Where(i => i.Code == IssueCatalog.CodeOffMillimetreGrid).ToList();

    private static string Named(IEnumerable<AnalysisIssue> issues) =>
        string.Join("; ", issues.Select(i => $"{i.Code} {i.Detail} — {i.Message}"));

    [Test]
    public void Create_BoardWithAnOddWidth_LeavesNoOffGridFinding()
    {
        var dims = new Vector3Int(537, 716, 18);
        Created(ElementFactory.CreatePart(dims, "Doska537",
            new Vector3(0f, dims.y * 0.5f * U, 0f)));

        var found = Grd01();

        CollectionAssert.IsEmpty(found,
            "ширина 537 нечётная, и центр на нуле ставит грани на ±268,5 мм — ровно то "
            + "состояние, в котором приложение деталь не оставляет: создание кончается "
            + "тем же выравниванием, что и отпускание мыши. " + Named(found));
    }

    [Test]
    public void Create_ScrewLegWithItsOddPadDiameter_LeavesNoOffGridFinding()
    {
        int bodyH = ScrewLegSpec.BodyHeightMM(ScrewLegSpec.DEFAULT_THREAD_LENGTH_MM,
            ScrewLegSpec.DEFAULT_BASE_HEIGHT_MM);
        Created(ElementFactory.CreateScrewLeg("Opora", new Vector3(0f, bodyH * 0.5f * U, 0f)));

        var found = Grd01();

        CollectionAssert.IsEmpty(found,
            $"пятак опоры Ø{ScrewLegSpec.DEFAULT_BASE_DIAMETER_MM} мм — нечётная ПОКУПНАЯ "
            + "величина, её не выбирал никто: на целом центре грани неизбежно встают на "
            + "половине, и убрать это может только конец жеста. " + Named(found));
    }

    [Test]
    public void Create_OvenWhoseFacadeIsNineteenAndAHalf_LeavesNoOffGridFinding()
    {
        Created(ElementFactory.CreateOven("Duhovka",
            new Vector3(0f, OvenElement.ModelDimensionsMM.y * 0.5f * U, 0f)));

        var found = Grd01();

        CollectionAssert.IsEmpty(found,
            "у духовки коробка ВАЛИДАЦИИ смещена на BODY_CENTER_Z = −9,75 мм — из фасада "
            + "толщиной 19,5 мм, — так что её грань стоит на четверти миллиметра. "
            + "Округляется именно эта коробка (GetVertices), а не голый трансформ: в "
            + "спецификацию и раскрой идёт она. " + Named(found));
    }

    [Test]
    public void Create_PutsTheShiftIntoTheSameUndoStepAsTheCreationItself()
    {
        var dims = new Vector3Int(537, 716, 18);
        var go = ElementFactory.CreatePart(dims, "Doska537", new Vector3(0f, dims.y * 0.5f * U, 0f));
        _spawned.Add(go);
        var element = go.GetComponent<KitchenElement>();
        var beforeCommit = element.transform.position;

        ElementCreation.Commit(go);

        Assert.AreNotEqual(beforeCommit.x, element.transform.position.x,
            "контроль стенда: на этой детали выравнивание обязано что-то сдвинуть, иначе "
            + "тест про «один шаг» не про что");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "создание и сдвиг — ОДНО действие пользователя: он нажал кнопку один раз. "
            + "Два шага в истории означали бы, что первый Ctrl+Z возвращает деталь мимо "
            + "сетки вместо того, чтобы убрать её вовсе");

        CommandStack.Undo();

        Assert.AreEqual(0, CommandStack.UndoCount, "один Ctrl+Z обязан убрать создание целиком");
        Assert.IsFalse(PartRegistry.GetAll().Contains(element),
            "и деталь обязана уйти со сцены, а не остаться сдвинутой");
    }

    [Test]
    public void Factory_WithoutTheGesture_LeavesThePartWhereItWasAsked()
    {
        var dims = new Vector3Int(537, 716, 18);
        var go = ElementFactory.CreatePart(dims, "Doska537", new Vector3(0f, dims.y * 0.5f * U, 0f));
        _spawned.Add(go);

        Assert.AreEqual(0f, go.transform.position.x, 1e-6f,
            "обратный вход: фабрика — builder, а не жест. Она обязана поставить деталь "
            + "ровно туда, куда просили, иначе выравнивание поедет и на ЗАГРУЗКЕ файла — "
            + "загрузка зовёт ту же фабрику, и правило «загрузка не двигает» держится "
            + "именно на этом");
    }
}
