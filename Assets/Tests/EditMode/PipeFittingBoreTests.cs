using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Меш фитинга следует за ВЫВЕДЕННЫМ диаметром.
///
/// До этого тело всегда строилось по номиналу ДУ 20, а панель показывала
/// настоящие числа: отвод «на глаз двадцатый», к которому подведена труба ДУ 50,
/// читается как ошибка чертежа, даже когда цифры под ним верны.
///
/// Повод пересчёта ОДИН и уже существовал — тот же проход, что считает выводимые
/// диаметры (SceneChangeTracker.SettleDerivedLinks → PipeFittingSizeLink). Второго
/// писателя у этих полей нет, поэтому и второй причины перестроить меш здесь не
/// заводится (CONVENTIONS.md → «A derived field needs ONE writer and one occasion
/// to call it»).
///
/// Несущая проверка набора — не «тело выросло», а «устья остались на месте».
/// Устья фитинга это его монтажный контракт: поедь они вслед за диаметром, стык,
/// из которого диаметр и выведен, разошёлся бы, диаметр пропал бы, рама вернулась
/// бы к номиналу — и фитинг замигал бы каждый кадр между ДУ 20 и ДУ 50. Поэтому
/// рама зафиксирована на номинале, а по проходу растёт только тело.</summary>
public class PipeFittingBoreTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private PipeElement Pipe(string name, int lengthMM, Vector3 pos, string sizeId)
    {
        var go = ElementFactory.CreatePipe(sizeId, lengthMM, name, pos);
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement Fitting(GameObject go)
    {
        _spawned.Add(go);
        return go.GetComponent<PipeFittingElement>();
    }

    /// <summary>Труба, чей ВЕРХНИЙ конец садится на устье фитинга: у порта трубы
    /// ось смотрит наружу, поэтому встречное устье обязано смотреть вниз.</summary>
    private PipeElement PipeUnder(string name, PipeFittingElement fitting, int portIndex,
        string sizeId, int lengthMM)
    {
        var mouth = fitting.PortPositionUnits(portIndex);
        return Pipe(name, lengthMM,
            new Vector3(mouth.x, mouth.y - Units(lengthMM * 0.5f), mouth.z), sizeId);
    }

    private PipeElement PipeOver(string name, PipeFittingElement fitting, int portIndex,
        string sizeId, int lengthMM)
    {
        var mouth = fitting.PortPositionUnits(portIndex);
        return Pipe(name, lengthMM,
            new Vector3(mouth.x, mouth.y + Units(lengthMM * 0.5f), mouth.z), sizeId);
    }

    private static Mesh MeshOf(PipeFittingElement fitting)
    {
        var filter = fitting.GetComponent<MeshFilter>();
        Assert.IsNotNull(filter, "у фитинга обязан быть меш: без него он не виден и не выбираем");
        Assert.IsNotNull(filter!.sharedMesh);
        return filter.sharedMesh;
    }

    /// <summary>Наибольший радиус тела по ту сторону ступицы, где лежит порт:
    /// половинки читаются порознь, иначе переходная муфта, нарисованная одним
    /// диаметром на обе стороны, прошла бы проверку по общему габариту.</summary>
    private static float BodyRadiusUnitsAround(Mesh mesh, float hubY, bool above)
    {
        float found = 0f;
        foreach (var v in mesh.vertices)
        {
            if (above ? v.y <= hubY : v.y >= hubY) continue;
            found = Mathf.Max(found, new Vector2(v.x, v.z).magnitude);
        }
        return found;
    }

    [Test]
    public void AnElbowFedByADn50Pipe_IsDrawnAtDn50_NotAtTheNominalTwenty()
    {
        var elbow = Fitting(ElementFactory.CreatePipeElbow("Elbow", Vector3.zero));
        float nominal = MeshOf(elbow).bounds.size.z;

        var riser = PipeUnder("Riser", elbow, 0, PipeSpec.Dn50, 600);

        Assert.AreEqual(1, PipeFittingSizeLink.ApplyAll(new KitchenElement[] { riser, elbow }),
            "выведенный диаметр отвода изменился — перестроить обязан ровно его");
        Assert.AreEqual(PipeSpec.Dn50, elbow.BoreSizeId,
            "к отводу подведена труба ДУ 50, и его проход теперь такой же");

        Assert.AreEqual(PipeFittingSpec.BodyDiameterMm(PipeSpec.Dn50) * AppConstants.MM_TO_UNITS,
            MeshOf(elbow).bounds.size.z, 1e-4f,
            "тело осталось номинальным: в свойствах 2 дюйма, а нарисованы 3/4 — "
            + "именно это и читается как ошибка чертежа");
        Assert.Greater(MeshOf(elbow).bounds.size.z, nominal);
    }

    [Test]
    public void TheMouthsOfAFitting_StayPut_WhenItsBoreIsDerived()
    {
        var elbow = Fitting(ElementFactory.CreatePipeElbow("Elbow", Vector3.zero));
        var before = new[] { elbow.PortPositionUnits(0), elbow.PortPositionUnits(1) };

        var riser = PipeUnder("Riser", elbow, 0, PipeSpec.Dn50, 600);
        var scene = new KitchenElement[] { riser, elbow };
        PipeFittingSizeLink.ApplyAll(scene);

        for (int i = 0; i < before.Length; i++)
            Assert.AreEqual(0f, Vector3.Distance(before[i], elbow.PortPositionUnits(i)), 1e-5f,
                "устье " + i + " уехало вслед за диаметром: стык, из которого диаметр "
                + "и выведен, на следующем проходе разойдётся, диаметр пропадёт, "
                + "и фитинг замигает между ДУ 20 и ДУ 50");

        Assert.AreEqual(0, PipeFittingSizeLink.ApplyAll(scene),
            "второй проход по неизменной сцене не находит ничего: перестраиваются "
            + "только те фитинги, у кого выведенный диаметр ИЗМЕНИЛСЯ");
        Assert.AreEqual(PipeSpec.Dn50, elbow.BoreSizeId,
            "и диаметр держится, а не сбрасывается обратно на номинал");
    }

    [Test]
    public void ATransitionCoupling_IsDrawnWithADifferentBodyOnEachSide()
    {
        var sleeve = Fitting(ElementFactory.CreatePipeCoupling("Sleeve", Vector3.zero));
        var thin = PipeUnder("Thin", sleeve, 0, PipeSpec.Dn20, 600);
        var thick = PipeOver("Thick", sleeve, 1, PipeSpec.Dn50, 600);

        PipeFittingSizeLink.ApplyAll(new KitchenElement[] { thin, sleeve, thick });

        CollectionAssert.AreEqual(new[] { PipeSpec.Dn20, PipeSpec.Dn50 }, sleeve.BoreSizeIds,
            "стороны переходной муфты независимы: снизу ДУ 20, сверху ДУ 50");

        var mesh = MeshOf(sleeve);
        float hubY = sleeve.transform.InverseTransformPoint(sleeve.HubPositionUnits).y;
        float toU = AppConstants.MM_TO_UNITS;

        Assert.AreEqual(PipeFittingSpec.BodyDiameterMm(PipeSpec.Dn20) * 0.5f * toU,
            BodyRadiusUnitsAround(mesh, hubY, false), 1e-4f,
            "нижняя половина обязана быть нарисована по своей трубе");
        Assert.AreEqual(PipeFittingSpec.BodyDiameterMm(PipeSpec.Dn50) * 0.5f * toU,
            BodyRadiusUnitsAround(mesh, hubY, true), 1e-4f,
            "а верхняя — по своей: одинаковые половинки означали бы, что переходная "
            + "муфта опять одноразмерная");
    }

    [Test]
    public void AFittingWithNothingConnected_KeepsTheNominalBore_AndShowsNoNumbers()
    {
        var cap = Fitting(ElementFactory.CreatePipeCap("Plug", Vector3.zero));
        PipeFittingSizeLink.ApplyAll(new KitchenElement[] { cap });

        Assert.AreEqual(PipeSpec.DEFAULT_SIZE, cap.BoreSizeId,
            "рисовать неподключённый фитинг нечем, кроме номинала");
        Assert.AreEqual(1, cap.BoreSizeIds.Count);
        Assert.IsNull(cap.BoreSizeIds[0],
            "но в свойствах обязан стоять прочерк, а не ДУ 20: номинал — это то, чем "
            + "его рисуют, а не измеренная величина");
    }

    [Test]
    public void ASceneWithoutAnyFitting_IsNotSurveyedAtAll()
    {
        var board = ElementFactory.CreatePart(new Vector3Int(600, 400, 18), "Board", Vector3.zero);
        _spawned.Add(board);

        Assert.AreEqual(0, PipeFittingSizeLink.ApplyAll(
                new[] { board.GetComponent<KitchenElement>() }),
            "проход обязан выйти на проверке «есть ли на сцене фитинг»: иначе каждый "
            + "кадр с движущимся элементом платит за обход всей сцены ради пустого ответа");
    }
}
