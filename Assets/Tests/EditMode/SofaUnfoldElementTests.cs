using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>Раскладывание дивана-книжки на настоящем элементе: два этапа СТРОГО
/// ПО ОЧЕРЕДИ — сначала сиденье выезжает и открывает короб (фото 45 → 43), потом
/// спинка заваливается на петле и ложится на короб кроватью (43 → 40).
///
/// Состояние ведёт то же устройство, что у ящика: <c>IOpenable</c> (кнопка в
/// панели, клавиша E, MCP cycle_drawer_animation), анимация в <c>Update</c>,
/// сохранение в проекте. Как и открытость ящика, этап раскладки НЕ откатывается
/// отменой — это показ, а не правка документа. Корневой transform дивана не
/// двигается вовсе: едут только дети, поэтому «закрытая поза» у него всегда.
///
/// Update в EditMode не вызывается, и тест ведёт время сам через
/// <c>Advance(секунды)</c> — тем же методом, которым пользуется Update.</summary>
public class SofaUnfoldElementTests
{
    private const float Mm = AppConstants.MM_TO_UNITS;
    private const float Eps = 1e-4f;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => LogAssert.ignoreFailingMessages = true;

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        LogAssert.ignoreFailingMessages = false;
    }

    private SofaElement Sofa(int width = SofaLayout.DefaultWidthMM,
        int depth = SofaLayout.DefaultDepthMM, int seatHeight = SofaLayout.DefaultSeatHeightMM)
    {
        var go = ElementFactory.CreateSofa(new Vector3Int(width, SofaLayout.OverallHeightMM, depth),
            SofaLayout.DefaultCornerRadiusMM, seatHeight, "Диван-раскладка", Vector3.zero);
        _spawned.Add(go);
        var sofa = go.GetComponent<SofaElement>();
        Assert.IsNotNull(sofa, "фабрика обязана вернуть SofaElement");
        return sofa!;
    }

    private static Transform Front(SofaElement sofa) => sofa.transform.Find(SofaLayout.FrontGroupName)!;

    private static Transform Hinge(SofaElement sofa) => sofa.transform.Find(SofaLayout.HingeGroupName)!;

    private static Transform Backrest(SofaElement sofa)
        => Hinge(sofa).Find(SofaLayout.BackrestName)!;

    private static float Travel(SofaElement sofa)
        => SofaUnfold.SeatSlideTravelMM(sofa.DimensionsMM.z, sofa.SeatHeightMM);

    private static float HingeAngle(SofaElement sofa)
        => Quaternion.Angle(Quaternion.identity, Hinge(sofa).localRotation);

    [Test]
    public void Sofa_Fresh_IsFoldedAndAtRest()
    {
        var sofa = Sofa();

        Assert.AreEqual(SofaStage.Folded, sofa.UnfoldStage, "диван создаётся сложенным");
        Assert.IsFalse(sofa.IsOpen, "и не открытым");
        Assert.AreEqual(Vector3.zero, Front(sofa).localPosition,
            "передняя группа на месте: сиденье не выдвинуто");
        Assert.AreEqual(0f, HingeAngle(sofa), Eps, "спинка стоит");
    }

    [Test]
    public void SnapToStage_Extended_SlidesTheWholeFrontGroupOutAndKeepsTheBackrestUp()
    {
        var sofa = Sofa();
        sofa.SnapToStage(SofaStage.Extended);

        Assert.AreEqual((Travel(sofa) + SofaUnfold.ExtraPullMM) * Mm,
            Front(sofa).localPosition.z, Eps,
            "сиденье выехало на ход выдвижения плюс запас на доступ к коробу");
        Assert.AreEqual(0f, HingeAngle(sofa), Eps,
            "на этапе 1 спинка ещё стоит: фото 43 — спинка вертикальна, сиденье выдвинуто");
    }

    [Test]
    public void SnapToStage_Extended_UncoversTheBox_BehindTheSlidSeat()
    {
        var sofa = Sofa();
        float boxFront = (-SofaLayout.DefaultDepthMM * 0.5f + SofaLayout.BackrestThicknessMM
            + SofaBoxLayout.RearSetbackMM + SofaBoxLayout.DepthMM(SofaLayout.DefaultDepthMM)) * Mm;

        sofa.SnapToStage(SofaStage.Extended);

        float seatRear = Part(sofa, SofaLayout.SeatName).GetComponent<MeshRenderer>()!.bounds.min.z;
        Assert.GreaterOrEqual(seatRear, boxFront + 0.019f,
            "задняя кромка выехавшего сиденья дальше передней стенки короба на зазор: "
            + "короб открыт целиком, и сиденье его не цепляет");
    }

    private static Transform Part(SofaElement sofa, string name)
    {
        var part = Front(sofa).Find(name);
        if (part == null) part = Hinge(sofa).Find(name);
        Assert.IsNotNull(part, "нет детали " + name);
        return part!;
    }

    [Test]
    public void SnapToStage_Bed_PutsTheBackrestWhereTheFormulaOfTheLayoutSays()
    {
        var sofa = Sofa();

        sofa.SnapToStage(SofaStage.Bed);

        var expected = SofaUnfold.BackrestCentreMM(sofa.DimensionsMM, sofa.SeatHeightMM,
            SofaUnfold.BackrestFlatAngleDeg) * Mm;
        var actual = sofa.transform.InverseTransformPoint(Backrest(sofa).position);
        Assert.AreEqual(expected.x, actual.x, Eps, "по ширине: спинка по центру");
        Assert.AreEqual(expected.y, actual.y, Eps,
            "по высоте: центр лежащей спинки там, где его считает формула поворота вокруг "
            + "петли. Формула живёт в быстром пути (там нет Quaternion), и именно этот тест "
            + "держит её в согласии с тем, что рисует Unity");
        Assert.AreEqual(expected.z, actual.z, Eps, "и по глубине");
    }

    [Test]
    public void SnapToStage_Bed_LiesFlatOnTheBox_FlushWithTheSeatTop()
    {
        var sofa = Sofa();

        sofa.SnapToStage(SofaStage.Bed);

        var back = Backrest(sofa).GetComponent<MeshRenderer>()!.bounds;
        var seat = Part(sofa, SofaLayout.SeatName).GetComponent<MeshRenderer>()!.bounds;
        Assert.AreEqual(SofaLayout.BackrestThicknessMM * Mm, back.size.y, Eps,
            "лежащая спинка — плита толщиной 180 мм");
        Assert.AreEqual(SofaLayout.BackrestHeightMM * Mm, back.size.z, Eps,
            "а её высота стала длиной кровати: 700 мм вдоль глубины");
        Assert.AreEqual(seat.max.y, back.max.y, Eps,
            "верх спинки вровень с верхом сиденья: фото 40 — обе половины кровати на одной "
            + "высоте");
        Assert.Greater(seat.min.z, back.max.z,
            "между спинкой и сиденьем зазор, а не наложение: они не врезаются друг в друга");
    }

    [Test]
    public void Advance_FromFoldedToBed_PullsTheSeatOutFirstAndTurnsTheBackrestAfter()
    {
        var sofa = Sofa();
        sofa.GoToStage(SofaStage.Bed);
        float fullPull = (Travel(sofa) + SofaUnfold.ExtraPullMM) * Mm;

        int steps = 0;
        bool moving = true;
        bool pulledOut = false;
        while (moving && steps < 100)
        {
            moving = sofa.Advance(0.1f);
            steps++;
            if (Front(sofa).localPosition.z > fullPull - Eps) pulledOut = true;

            if (HingeAngle(sofa) > 1e-2f)
                Assert.IsTrue(pulledOut,
                    "спинка начала заваливаться, а сиденье ещё не дошло до полного выдвижения: "
                    + "этапы перекрылись, шаг " + steps);
        }

        Assert.GreaterOrEqual(steps, 20,
            "два этапа по секунде при шаге 0,1 дают не меньше двадцати шагов — меньше "
            + "значит анимация проскочила одним прыжком");
        Assert.IsFalse(moving, "анимация закончилась");
        Assert.AreEqual(90f, HingeAngle(sofa), 1e-2f, "спинка легла");
        Assert.AreEqual(Travel(sofa) * Mm, Front(sofa).localPosition.z, Eps,
            "а сиденье подъехало обратно к спинке и стоит в зазоре кровати");
    }

    [Test]
    public void Advance_FromBedToFolded_RaisesTheBackrestFirstAndTucksTheSeatAfter()
    {
        var sofa = Sofa();
        sofa.SnapToStage(SofaStage.Bed);
        sofa.GoToStage(SofaStage.Folded);

        int steps = 0;
        while (sofa.Advance(0.1f) && steps < 100)
        {
            steps++;
            if (Front(sofa).localPosition.z < Travel(sofa) * Mm - Eps)
                Assert.AreEqual(0f, HingeAngle(sofa), 1e-2f,
                    "сиденье уехало глубже положения кровати, а спинка ещё не встала: "
                    + "складывание идёт тем же порядком наоборот, шаг " + steps);
        }

        Assert.AreEqual(0f, Front(sofa).localPosition.z, Eps, "сиденье вернулось");
        Assert.AreEqual(0f, HingeAngle(sofa), 1e-2f, "спинка стоит");
    }

    [Test]
    public void CycleOpenState_StepsThroughTheStages_AndBackToFolded()
    {
        var sofa = Sofa();

        sofa.CycleOpenState();
        Assert.AreEqual(SofaStage.Extended, sofa.UnfoldStage, "первое нажатие — выдвинуть");
        Assert.IsTrue(sofa.IsOpen, "диван считается открытым");

        sofa.CycleOpenState();
        Assert.AreEqual(SofaStage.Bed, sofa.UnfoldStage, "второе — разложить");

        sofa.CycleOpenState();
        Assert.AreEqual(SofaStage.Folded, sofa.UnfoldStage, "третье — сложить");
        Assert.IsFalse(sofa.IsOpen, "и снова закрыт");
    }

    [Test]
    public void ToggleOpen_GoesStraightBetweenFoldedAndTheBed()
    {
        var sofa = Sofa();

        sofa.ToggleOpen();
        Assert.AreEqual(SofaStage.Bed, sofa.UnfoldStage,
            "переключатель открыто/закрыто (его зовёт общий код ящиков) раскладывает целиком");

        sofa.ToggleOpen();
        Assert.AreEqual(SofaStage.Folded, sofa.UnfoldStage, "и складывает целиком");
    }

    [Test]
    public void OpenActionLabel_NamesTheNextAction_AtEveryStage()
    {
        var sofa = Sofa();
        var labels = new List<string>();

        foreach (var stage in new[] { SofaStage.Folded, SofaStage.Extended, SofaStage.Bed })
        {
            sofa.SnapToStage(stage);
            labels.Add(sofa.OpenActionLabel);
        }

        CollectionAssert.AllItemsAreUnique(labels,
            "на каждом этапе кнопка обещает своё действие: «выдвинуть», «разложить», "
            + "«сложить». Одна подпись на два этапа заставила бы гадать, что произойдёт");
        Assert.AreEqual(OpenLabels.SofaExtend, labels[0], "со сложенного — выдвинуть");
        Assert.AreEqual(OpenLabels.SofaUnfold, labels[1], "с выдвинутого — разложить");
        Assert.AreEqual(OpenLabels.SofaFold, labels[2], "с кровати — сложить");
    }

    [Test]
    public void ForceClose_LeavesTheStageAlone_BecauseTheRootNeverMoves()
    {
        var sofa = Sofa();
        sofa.SnapToStage(SofaStage.Bed);

        sofa.ForceClose();

        Assert.AreEqual(SofaStage.Bed, sofa.UnfoldStage,
            "ForceClose нужен элементам, у которых ДВИГАЕТСЯ корень (ящик, дверь), чтобы "
            + "прочитать их логическую позу; у дивана корень неподвижен. Иначе правка любого "
            + "поля в панели (её обработчик зовёт ForceClose) складывала бы диван");
        Assert.IsTrue(sofa.IsClosedPose,
            "и поле положения читается по корню без оглядки на этап");
    }

    [Test]
    public void Resize_WhileExtended_KeepsThePose_AndRecomputesTheTravelForTheNewDepth()
    {
        var sofa = Sofa();
        sofa.SnapToStage(SofaStage.Extended);

        sofa.DimensionsMM = new Vector3Int(2000, 800, 1200);

        Assert.AreEqual((Travel(sofa) + SofaUnfold.ExtraPullMM) * Mm,
            Front(sofa).localPosition.z, Eps,
            "после растяжения вглубь сиденье остаётся выдвинутым, а ход пересчитан: короб "
            + "стал длиннее, и прежний ход открыл бы его не целиком");
        Assert.AreEqual(SofaStage.Extended, sofa.UnfoldStage, "этап не сбросился");
    }

    [Test]
    public void SeatHeight_WhileBed_KeepsTheBackrestFlushWithTheNewSeatTop()
    {
        var sofa = Sofa();
        sofa.SnapToStage(SofaStage.Bed);

        sofa.SeatHeightMM = 300;

        var back = Backrest(sofa).GetComponent<MeshRenderer>()!.bounds;
        float seatTop = (-SofaLayout.OverallHeightMM * 0.5f + 300) * Mm;
        Assert.AreEqual(seatTop, back.max.y, Eps,
            "петля переехала вместе с верхом короба, и лежащая спинка осталась вровень с "
            + "новым верхом сиденья");
        Assert.AreEqual(90f, HingeAngle(sofa), 1e-2f, "и осталась лежащей");
    }

    [Test]
    public void Stage_SurvivesSaveAndLoad_AsTheBedPoseWithoutAnimation()
    {
        var sofa = Sofa();
        sofa.SnapToStage(SofaStage.Bed);

        var data = ElementCapture.FromElement(sofa);
        var restored = JsonUtility.FromJson<ElementData>(JsonUtility.ToJson(data));
        Assert.AreEqual((int)SofaStage.Bed, restored.sofaUnfoldStage,
            "этап записан в проект: открытый ящик переживает сохранение, и диван — тоже");

        var go = ElementRestorers.Restore(ElementFactory.Instance, restored);
        _spawned.Add(go);
        var loaded = go.GetComponent<SofaElement>()!;

        Assert.AreEqual(SofaStage.Bed, loaded.UnfoldStage, "этап восстановлен");
        Assert.AreEqual(90f, HingeAngle(loaded), 1e-2f,
            "и поза сразу кроватная: загруженный проект не должен проигрывать раскладывание "
            + "заново при открытии");
    }

    [Test]
    public void Stage_OfAFoldedSofa_IsWrittenAsZero_SoOtherTypesCannotInheritIt()
    {
        var sofa = Sofa();

        var data = ElementCapture.FromElement(sofa);

        Assert.AreEqual(0, data.sofaUnfoldStage, "сложенный диван пишет ноль");

        var stool = ElementFactory.CreateStool(new Vector3Int(360, 450, 360), 0, "Табуретка-S",
            Vector3.zero);
        _spawned.Add(stool);
        Assert.AreEqual(0, ElementCapture.FromElement(stool.GetComponent<KitchenElement>()).sofaUnfoldStage,
            "у не-дивана поле остаётся нулём");
    }

    [Test]
    public void Stage_OfACorruptedSave_IsClampedToTheNearestRealStage()
    {
        var data = ElementCapture.FromElement(Sofa());
        data.sofaUnfoldStage = 99;

        var go = ElementRestorers.Restore(ElementFactory.Instance, data);
        _spawned.Add(go);

        Assert.AreEqual(SofaStage.Bed, go.GetComponent<SofaElement>()!.UnfoldStage,
            "число за пределами не должно увезти диван в несуществующую позу");
    }

    private static bool CushionsShown(SofaElement sofa)
    {
        return Front(sofa).Find(SofaLayout.BackCushionLeftName)!.gameObject.activeInHierarchy;
    }

    [Test]
    public void Cushions_AreTakenOffAtTheStartOfTheUnfold_AndPutBackWhenFoldedAgain()
    {
        var sofa = Sofa();
        Assert.IsTrue(CushionsShown(sofa), "сложенный диван с подушками");

        sofa.GoToStage(SofaStage.Extended);
        sofa.Advance(0.1f);
        Assert.IsFalse(CushionsShown(sofa),
            "сиденье только тронулось, а подушек уже нет: на фото 43 их на сиденье нет, и они "
            + "закрыли бы короб и пересекли бы спинку");
        sofa.Advance(10f);
        Assert.IsFalse(CushionsShown(sofa), "на этапе 1 их по-прежнему нет");

        sofa.GoToStage(SofaStage.Folded);
        sofa.Advance(0.5f);
        Assert.IsFalse(CushionsShown(sofa), "по дороге обратно подушек нет");
        sofa.Advance(10f);
        Assert.IsTrue(CushionsShown(sofa),
            "а когда сиденье встало на место, подушки вернулись: иначе сложенный диван "
            + "навсегда остался бы голым");
    }

    [Test]
    public void Cushions_StayOff_WhenTheSceneVisibilityManagerSwitchesEveryRendererOnAgain()
    {
        var sofa = Sofa();
        sofa.SnapToStage(SofaStage.Extended);

        SceneVisibility.SetRenderersEnabled(sofa, true);

        Assert.IsFalse(CushionsShown(sofa),
            "SceneVisibilityManager.LateUpdate включает ВСЕ рендереры элемента при любой "
            + "смене уровня или реестра — раньше подушки прятались флагом рендерера, и кадр "
            + "«выдвинуто» показывал их на сиденье. Прячет теперь сам объект (SetActive), и "
            + "включить его обратно рендерером нельзя");
    }

    [Test]
    public void Cushions_StayOffAfterARebuild_WhileTheSofaIsUnfolded()
    {
        var sofa = Sofa();
        sofa.SnapToStage(SofaStage.Bed);

        sofa.SeatHeightMM = 400;

        Assert.IsFalse(CushionsShown(sofa),
            "пересборка (правка высоты сиденья) создаёт подушки заново, и они обязаны "
            + "родиться скрытыми: иначе правка в панели «возвращала» подушки на кровать");
    }

    [Test]
    public void Backrest_MidTurn_IsWhereTheFormulaOfTheLayoutSays_IncludingTheShiftFromTheWall()
    {
        var sofa = Sofa();
        sofa.GoToStage(SofaStage.Bed);
        sofa.Advance(1.3f);

        var pose = SofaUnfold.PoseAt(1.3f, sofa.DimensionsMM.z, sofa.SeatHeightMM);
        var expected = SofaUnfold.BackrestCentreMM(sofa.DimensionsMM, sofa.SeatHeightMM,
            pose.BackrestAngleDeg) * Mm;
        var actual = sofa.transform.InverseTransformPoint(Backrest(sofa).position);

        Assert.Greater(pose.BackrestShiftMM, 1f,
            "на этом угле поправка от стены ненулевая — иначе тест не проверял бы её");
        Assert.AreEqual(expected.y, actual.y, Eps, "по высоте");
        Assert.AreEqual(expected.z, actual.z, Eps,
            "по глубине, с поправкой: формула слоя геометрии и то, что рисует Unity, сходятся "
            + "и в середине поворота, а не только на концах");
    }

    [Test]
    public void Backrest_WhileTurning_NeverLeavesTheBackWallPlane_OnTheRealElement()
    {
        var sofa = Sofa();
        float wall = -sofa.DimensionsMM.z * 0.5f * Mm;
        sofa.SnapToStage(SofaStage.Extended);
        sofa.GoToStage(SofaStage.Bed);

        int steps = 0;
        while (sofa.Advance(0.02f) && steps < 200)
        {
            steps++;
            var bounds = Backrest(sofa).GetComponent<MeshRenderer>()!.bounds;
            Assert.GreaterOrEqual(bounds.min.z, wall - 1e-3f,
                "габарит спинки по Z не заходит за заднюю плоскость дивана на шаге " + steps
                + " (AABB повёрнутой детали консервативна, поэтому допуск 1 мм)");
        }

        Assert.Greater(steps, 40, "поворот прошёл за сорок шагов — тест ничего не проверил бы");
    }

    [Test]
    public void Sofa_IsActivatedByTheHotkeyLikeADrawer()
    {
        var sofa = Sofa();

        Assert.AreEqual(ActivationKind.Openable, ElementActivator.KindOf(sofa),
            "клавиша E раскладывает диван тем же правилом, что открывает ящик (двойной щелчок "
            + "для открываемых элементов не включён нигде, и диван не исключение)");
        Assert.IsTrue(ElementActivator.Activate(sofa), "активация проходит");
        Assert.AreEqual(SofaStage.Extended, sofa.UnfoldStage, "и делает следующий этап");
    }
}
