using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Одно место, из которого берут сторону съёмки ОБА съёмщика — и
/// миниатюра каталога, и изометрический кадр.
///
/// Раньше сторон было две. Миниатюра спрашивала
/// <c>ElementFacing.CameraDirection(IsoDir)</c> и получалась правильной сразу,
/// а изометрия ставила камеру по сырому <c>IsoDir</c> — то есть в затылок — и
/// лечилась поимённо: константа разворота на 180° была
/// выписана у дивана, кровати, настенной арматуры и стиральных машин, а
/// посудомойке, духовке и варочной никто её не выписал. Список случаев,
/// которые заметили глазами, — это не правило (<c>conventions/CORRECTNESS.md</c>
/// → «State a rule by its MECHANISM, not as a list of the cases you happened to
/// fix»).
///
/// Теперь сторона одна: <see cref="IsoCameraRig.ViewDir"/> выводится из
/// объявленной фронтальной оси, и <see cref="IsoCameraRig.Position"/> ставит
/// камеру только по ней. Разворачивать элемент не нужно ни одному типу, и
/// новый тип накрыт до того, как его снимут.</summary>
public class IsoCameraRigViewTests
{
    [Test]
    public void ViewDir_IsTheFacingAnswer_NotTheRawIsoVector()
    {
        Assert.AreEqual(ElementFacing.CameraDirection(IsoCameraRig.IsoDir), IsoCameraRig.ViewDir,
            "сторона съёмки выводится из ElementFacing.LocalFront, а не задаётся вторым "
            + "вектором рядом: два вектора разъехались бы молча, ровно как разъехались "
            + "миниатюра и изометрия");

        Assert.AreNotEqual(IsoCameraRig.IsoDir, IsoCameraRig.ViewDir,
            "и они РАЗНЫЕ: пока лицо смотрит в +Z, а сырой IsoDir — со стороны -Z, "
            + "камера обязана перейти на другую сторону. Совпадение здесь означало бы, "
            + "что вывод перестал работать и снова снимают затылок");
    }

    [Test]
    public void CameraStandsOnTheSideTheFrontFaces_ForEveryTypeAtOnce()
    {
        Assert.Greater(Vector3.Dot(ElementFacing.LocalFront, IsoCameraRig.ViewDir), 0f,
            "объектив стоит там, куда смотрит лицо: это и есть замена поимённым "
            + "разворотам");
    }

    [Test]
    public void Position_PlacesTheCameraAlongViewDir_SoNoShooterCanUseTheRawVector()
    {
        var centre = new Vector3(1.3f, 0.7f, -2.1f);
        var size = new Vector3(0.6f, 0.9f, 0.4f);

        var placed = IsoCameraRig.Position(centre, size, 2.5f);
        var offset = placed - centre;

        Assert.AreEqual(IsoCameraRig.Distance(size, 2.5f, IsoCameraRig.MinDistance),
            offset.magnitude, 1e-4f,
            "дистанция кадрирования не изменилась — переехала только сторона");
        Assert.Greater(Vector3.Dot(offset.normalized, ElementFacing.LocalFront), 0f,
            "а камера уехала на сторону лица. Тест держит именно Position, потому что "
            + "через неё ходит съёмщик изометрии: вернись она к IsoDir — кадры снова "
            + "стали бы затылочными, и заметил бы это глаз, а не прогон");
    }

    [Test]
    public void ViewDir_KeepsTheThreeQuarterElevation_SoTheFrameStaysIsometric()
    {
        Assert.AreEqual(IsoCameraRig.IsoDir.y, IsoCameraRig.ViewDir.y, 1e-4f,
            "переезд идёт вокруг вертикали: ракурс остаётся тем же 3/4 сверху, и "
            + "кадрирование эталонов не меняет характера — меняется только сторона");
    }
}
