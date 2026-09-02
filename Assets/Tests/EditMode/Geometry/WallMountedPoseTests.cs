using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Посадка элемента НА поверхность стены — без проёма и без выреза, в отличие
/// от окна и двери. Считается тремя чистыми функциями, потому что под dotnet
/// доступна векторная арифметика, но НЕ конструкторы поворотов: угол здесь
/// отдаётся числом в градусах, а Quaternion собирает уже адаптер в сцене.
///
/// Главное, что тут стережётся, — сторона стены. Нормаль плоскости
/// двусторонняя, и выбор «наружу» обязан следовать за тем, с какой стороны
/// элемент СЕЙЧАС: иначе унитаз, поставленный с другой стороны перегородки,
/// прыгает сквозь неё в соседнюю комнату.
/// </summary>
public class WallMountedPoseTests
{
    private static readonly Vector3 WallCentre = new Vector3(2f, 1.25f, -3f);

    [Test]
    public void OutwardNormal_PointsToTheSideTheElementIsOn()
    {
        var plane = Vector3.forward;

        Assert.AreEqual(Vector3.forward,
            WallMountedPose.OutwardNormal(plane, WallCentre, WallCentre + Vector3.forward),
            "элемент перед стеной — нормаль наружу совпадает с плоскостной");
        Assert.AreEqual(-Vector3.forward,
            WallMountedPose.OutwardNormal(plane, WallCentre, WallCentre - Vector3.forward),
            "элемент за стеной — нормаль обязана перевернуться, иначе он "
            + "перепрыгивает сквозь стену в соседнюю комнату");
    }

    [Test]
    public void OutwardNormal_IgnoresHeight()
    {
        var high = WallCentre + new Vector3(0f, 5f, 0.4f);
        Assert.AreEqual(Vector3.forward,
            WallMountedPose.OutwardNormal(Vector3.forward, WallCentre, high),
            "сторону стены решает только план: элемент под потолком и элемент у пола "
            + "с одной стороны — это одна сторона");
    }

    [Test]
    public void OutwardNormal_IsHorizontalAndUnitLength()
    {
        var tilted = new Vector3(3f, 7f, 4f);
        var n = WallMountedPose.OutwardNormal(tilted, WallCentre,
            WallCentre + new Vector3(1f, 0f, 1f));

        Assert.AreEqual(0f, n.y, 1e-5f, "вертикальная составляющая нормали стены — мусор");
        Assert.AreEqual(1f, n.magnitude, 1e-5f, "нормаль обязана быть единичной: на неё "
            + "умножается отступ, и ненормированная тихо промахивается по расстоянию");
    }

    [Test]
    public void OutwardNormal_OfADegenerateWall_FallsBackInsteadOfDividingByZero()
    {
        Assert.AreEqual(Vector3.forward,
            WallMountedPose.OutwardNormal(Vector3.up, WallCentre, WallCentre),
            "чисто вертикальная «нормаль» и совпадающие точки не имеют плановой "
            + "стороны; результат обязан быть конечным вектором, а не NaN");
    }

    [Test]
    public void SeatedPosition_PutsTheElementAtExactlyTheStandoff()
    {
        var n = Vector3.forward;
        var far = WallCentre + new Vector3(0.7f, 0.9f, 3f);

        var seated = WallMountedPose.SeatedPosition(far, WallCentre, n, 0.32f);

        Assert.AreEqual(0.32f, Vector3.Dot(seated - WallCentre, n), 1e-5f,
            "по нормали элемент обязан встать ровно на отступ: полтолщины стены плюс "
            + "полглубины элемента, иначе он утапливается в стену или висит в воздухе");
    }

    [Test]
    public void SeatedPosition_KeepsHeightAndSlide()
    {
        var n = Vector3.forward;
        var somewhere = WallCentre + new Vector3(1.4f, 0.65f, 2f);

        var seated = WallMountedPose.SeatedPosition(somewhere, WallCentre, n, 0.3f);

        Assert.AreEqual(somewhere.x, seated.x, 1e-5f,
            "вдоль стены элемент ездит свободно — привязка не имеет права его центрировать");
        Assert.AreEqual(somewhere.y, seated.y, 1e-5f,
            "высоту задаёт пользователь и собственный параметр установки, а не стена");
    }

    [Test]
    public void SeatedPosition_PullsBackAnElementSunkIntoTheWall()
    {
        var n = Vector3.forward;
        var inside = WallCentre + new Vector3(0f, 0f, 0.02f);

        var seated = WallMountedPose.SeatedPosition(inside, WallCentre, n, 0.3f);

        Assert.AreEqual(0.3f, Vector3.Dot(seated - WallCentre, n), 1e-5f,
            "элемент, затащенный внутрь стены, обязан выехать наружу — притягивать "
            + "нужно в обе стороны, а не только издалека");
    }

    [Test]
    public void SeatedPosition_IsIdempotent()
    {
        var n = Vector3.forward;
        var once = WallMountedPose.SeatedPosition(
            WallCentre + new Vector3(0.4f, 0.5f, 1.1f), WallCentre, n, 0.27f);
        var twice = WallMountedPose.SeatedPosition(once, WallCentre, n, 0.27f);

        Assert.AreEqual(once, twice,
            "привязка зовётся на каждом кадре с изменившейся позой: неидемпотентная "
            + "тихо уползала бы от стены сама по себе");
    }

    [Test]
    public void YawDegrees_TurnsTheFrontAwayFromTheWall()
    {
        Assert.AreEqual(0f, WallMountedPose.YawDegrees(Vector3.forward), 1e-4f,
            "нормаль на +Z — элемент смотрит вперёд, поворот нулевой");
        Assert.AreEqual(90f, WallMountedPose.YawDegrees(Vector3.right), 1e-4f,
            "стена слева — унитаз развёрнут на четверть по часовой");
        Assert.AreEqual(-90f, WallMountedPose.YawDegrees(Vector3.left), 1e-4f,
            "стена справа — знак поворота обязан быть противоположным, иначе элемент "
            + "встаёт лицом в стену");
        Assert.AreEqual(180f, Mathf.Abs(WallMountedPose.YawDegrees(Vector3.back)), 1e-4f,
            "спиной к стене на -Z: знак ±180 неважен, важна сама полуоборотность");
    }
}
