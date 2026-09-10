using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Фронтальная ось элемента и сторона, с которой её снимает камера
/// миниатюры.
///
/// Дефект, из-за которого тесты появились: «стул и унитаз на миниатюре
/// спинкой к зрителю». Разворачивать эти два типа было бы починкой не там.
/// Лицо в проекте у ВСЕХ типов смотрит в +Z — спинка стула и бачок унитаза
/// стоят в -Z, спинка дивана там же, ручка духовки торчит в +Z, настенная
/// арматура садится задней гранью на z=0 и растёт в +Z. А общая изометрическая
/// камера (`IsoCameraRig.IsoDir`) стоит со стороны -Z, то есть В ЗАТЫЛОК
/// каждому элементу без исключения. Просто у шкафа затылок похож на лицо, а у
/// стула нет.
///
/// Поэтому разворот один и вывОдится, а не вписан числом: камера миниатюры
/// встаёт на ту сторону, куда смотрит объявленная фронтальная ось. Ни один тип
/// не заводит своего числа, значит новый тип накрыт сам, а перевёрнутый когда-
/// нибудь `IsoDir` не потребует правки — ответ поедет за ним.
///
/// Общий `IsoDir` здесь НЕ правится: он же кадрирует изометрические эталоны
/// (`IsoScreenshotTests`), и его правка пересняла бы каждый чужой снимок.</summary>
public class ElementFacingTests
{
    [Test]
    public void LocalFront_IsPlusZ_TheOneConventionEveryTypeFollows()
    {
        Assert.AreEqual(Vector3.forward, ElementFacing.LocalFront,
            "лицо элемента смотрит в +Z: на это соглашение опираются спинка стула, "
            + "спинка дивана, бачок унитаза, ручка духовки и посадка настенной арматуры");
    }

    /// <summary>Второй конец соглашения, взятый из НАСТОЯЩЕЙ геометрии соседей,
    /// а не из прозы: пока лицо у всех типов смотрит в одну сторону, разворот
    /// миниатюры имеет право быть один на всех. Разъедется соглашение — красное
    /// покажет на тип, который поехал, и на то, что общий разворот стал вредным.</summary>
    [Test]
    public void EveryNeighbourFacesTheSameWay_SoOneTurnServesEveryType()
    {
        Assert.Less(ToiletLayout.CisternCentreZMM, 0f,
            "бачок унитаза стоит в -Z — сзади");
        Assert.Greater(ToiletLayout.BowlCentreZMM, ToiletLayout.CisternCentreZMM,
            "а чаша впереди бачка: лицо унитаза смотрит в +Z");

        var sofaDims = new Vector3Int(SofaLayout.DefaultWidthMM, SofaLayout.DefaultHeightMM,
            SofaLayout.DefaultDepthMM);
        Assert.Less(SofaLayout.BackRail(sofaDims, SofaLayout.DefaultSeatHeightMM).CentreMM.z, 0f,
            "спинка дивана — там же, в -Z, то есть и его лицо смотрит в +Z: "
            + "стул с унитазом не выделяются, у них та же ориентация");

        float frontmostDoorZ = float.NegativeInfinity;
        foreach (var (centerMM, _) in OvenBody.DoorPartsMM())
            if (centerMM.z > frontmostDoorZ) frontmostDoorZ = centerMM.z;

        Assert.Greater(frontmostDoorZ, OvenBody.TOTAL_DEPTH_MM * 0.5f,
            "ручка духовки торчит ВПЕРЁД, за плоскость фасада — в +Z: третий "
            + "независимый тип с тем же соглашением");
    }

    [Test]
    public void IsoCamera_StandsBehindTheFront_SoTheThumbnailTurnsHalfATurn()
    {
        Assert.Less(Vector3.Dot(ElementFacing.LocalFront, IsoCameraRig.IsoDir), 0f,
            "общая изометрическая камера стоит со стороны -Z, то есть в затылок лицу: "
            + "это и есть причина разворота, а не свойство стула с унитазом");

        Assert.AreEqual(180f, ElementFacing.YawTowardsCameraDeg(IsoCameraRig.IsoDir), 1e-3f,
            "значит камера миниатюры обязана перейти на противоположную сторону — "
            + "ровно полразворота");

        Assert.Greater(
            Vector3.Dot(ElementFacing.LocalFront,
                ElementFacing.CameraDirection(IsoCameraRig.IsoDir)), 0f,
            "и после перехода лицо смотрит в объектив");
    }

    [Test]
    public void MirroredIsoDirection_NeedsNoTurnAtAll()
    {
        var fromTheFront = new Vector3(0.5f, 0.5f, 0.866f).normalized;

        Assert.AreEqual(0f, ElementFacing.YawTowardsCameraDeg(fromTheFront), 1e-3f,
            "противоположный вход: камера, уже стоящая со стороны лица, разворота не "
            + "требует. Без этой проверки формула могла бы просто возвращать 180 "
            + "всегда — и была бы зелёной ровно до первой правки IsoDir");
    }

    [Test]
    public void CameraDirection_KeepsTheCameraHeightAndOnlyTurnsItAroundTheVerticalAxis()
    {
        var turned = ElementFacing.CameraDirection(IsoCameraRig.IsoDir);

        Assert.AreEqual(IsoCameraRig.IsoDir.y, turned.y, 1e-4f,
            "разворот идёт вокруг вертикали: высота камеры над элементом не меняется, "
            + "иначе поехал бы весь изометрический характер кадра");
        Assert.AreEqual(IsoCameraRig.IsoDir.magnitude, turned.magnitude, 1e-4f,
            "и это поворот, а не отражение: длина направления сохраняется");
        Assert.Greater(turned.z, 0f, "камера переезжает на сторону лица, то есть в +Z");
    }

    [Test]
    public void YawTowardsCamera_SnapsToQuarterTurns_SoTheFrameKeepsItsThreeQuarterAngle()
    {
        Assert.AreEqual(90f, ElementFacing.YawTowardsCameraDeg(new Vector3(1f, 0.5f, 0f)), 1e-3f,
            "камера сбоку по +X — четверть разворота");
        Assert.AreEqual(270f, ElementFacing.YawTowardsCameraDeg(new Vector3(-1f, 0.5f, 0f)), 1e-3f,
            "и с другого боку — три четверти, а не минус одна");
        Assert.AreEqual(0f, ElementFacing.YawTowardsCameraDeg(new Vector3(0.1f, 0.5f, 1f)), 1e-3f,
            "почти фронтальный вид не доворачивается до строго фронтального: "
            + "снимок обязан остаться трёхчетвертным, иначе миниатюра станет плоской");
    }

    [Test]
    public void StraightFromAbove_HasNoHorizontalSide_SoNothingTurns()
    {
        Assert.AreEqual(0f, ElementFacing.YawTowardsCameraDeg(Vector3.up), 1e-3f,
            "у взгляда сверху вниз нет горизонтальной стороны, и выбирать её не из чего: "
            + "нормировать нулевой вектор — это NaN в позиции камеры");
        Assert.AreEqual(Vector3.up, ElementFacing.CameraDirection(Vector3.up),
            "а направление обязано остаться собой, а не превратиться в NaN");
    }
}
