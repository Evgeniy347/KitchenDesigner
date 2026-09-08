using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Plumbing;

/// <summary>Задача D: поворот присоединённого фитинга (кнопки поворота в контекстном
/// меню, ContextMenuUI.RotateAxis) обязан, если это вообще возможно, СДВИНУТЬ деталь
/// так, чтобы после поворота один из её портов снова сел устье в устье на ту же трубу
/// — то же правило «максимум связей», что и в задаче C
/// (<see cref="PipeFittingSeatChoice"/> для новой детали, <c>PipeDocking.ReseatAfterRotation</c>
/// здесь), просто с зафиксированным поворотом вместо перебора доворота.
///
/// Оба теста поворачивают ОДИН и тот же засеянный отвод на 90°, отличаясь только осью
/// поворота: вокруг оси стыка (вертикаль) нулевой порт остаётся смотреть туда же и
/// только съезжает — его обязаны вернуть; вокруг горизонтальной оси ни один порт
/// больше не смотрит на трубу — двигать некуда, и деталь остаётся как повёрнута.</summary>
public class PipeFittingRotationLinksTests : SnapTestBase
{
    private const int PipeLengthMm = 600;
    private const int LowerEnd = 0;

    [TearDown]
    public void ClearScene()
    {
        CommandStack.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static float Units(float mm) => mm * AppConstants.MM_TO_UNITS;

    private PipeElement PipeWithItsLowerEndAt(Vector3 end, string name)
    {
        var go = ElementFactory.CreatePipe(PipeSpec.DEFAULT_SIZE, PipeLengthMm, name,
            end + new Vector3(0f, Units(PipeLengthMm * 0.5f), 0f));
        _spawned.Add(go);
        return go.GetComponent<PipeElement>();
    }

    private PipeFittingElement SeatedElbow(PipeElement pipe)
    {
        PipeEndFittings.Set(pipe, LowerEnd, PipeNodeKind.Elbow, PartRegistry.GetAll());
        foreach (var e in PartRegistry.GetAll())
            if (!_spawned.Contains(e.gameObject)) _spawned.Add(e.gameObject);
        var elbow = PipeEndFittings.NeighbourAt(pipe, LowerEnd, PartRegistry.GetAll())
            as PipeFittingElement;
        Assert.IsNotNull(elbow, "стенд обязан доказать сам себя: отвод сел на трубу");
        return elbow!;
    }

    private static int JoinedLinks() =>
        PipeNetwork.Build(new ScenePipeSnapshot(PartRegistry.GetAll()).Ports()).Links.Count;

    [Test]
    public void ReseatAfterRotation_TranslatesTheFitting_WhenAPortCanStillReachTheSameMouth()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = SeatedElbow(pipe);
        Assume.That(JoinedLinks(), Is.EqualTo(1), "отвод сидит на трубе одним портом");
        var remembered = PipeDocking.ConnectedMouths(elbow, PartRegistry.GetAll());
        Assume.That(remembered.Count, Is.EqualTo(1));

        elbow.transform.rotation = Quaternion.AngleAxis(90f, Vector3.up) * elbow.transform.rotation;
        Assume.That(JoinedLinks(), Is.EqualTo(0),
            "поворот сам по себе рвёт стык — это и есть баг задачи D до правки");

        bool moved = PipeDocking.ReseatAfterRotation(elbow, remembered);

        Assert.IsTrue(moved,
            "поворот вокруг ВЕРТИКАЛИ — той же оси, на которой стоит стык с трубой — не "
            + "уводит нулевой порт от направления «вверх», он только съезжает вместе с "
            + "деталью; доворот обязан подтянуть деталь обратно");
        Assert.AreEqual(1, JoinedLinks(), "стык с трубой восстановлен");
    }

    [Test]
    public void ReseatAfterRotation_LeavesTheFittingInPlace_WhenNoPortCanReachTheMouthAnymore()
    {
        var pipe = PipeWithItsLowerEndAt(Vector3.zero, "Run");
        var elbow = SeatedElbow(pipe);
        var remembered = PipeDocking.ConnectedMouths(elbow, PartRegistry.GetAll());
        Assume.That(remembered.Count, Is.EqualTo(1));
        Vector3 posBeforeReseat = elbow.transform.position;

        elbow.transform.rotation = Quaternion.AngleAxis(90f, Vector3.right) * elbow.transform.rotation;

        bool moved = PipeDocking.ReseatAfterRotation(elbow, remembered);

        Assert.IsFalse(moved,
            "поворот вокруг ГОРИЗОНТАЛЬНОЙ оси уводит ОБА порта отвода от направления "
            + "«вверх» — ни один не смотрит больше туда, где труба, и двигать деталь "
            + "некуда: она остаётся ровно как повёрнута");
        Assert.AreEqual(posBeforeReseat, elbow.transform.position,
            "позиция не тронута, когда сдвигать некуда");
    }
}
