using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Винтовая опора Ø25 под торцом бока 16 мм: прилипание обязано ставить
/// её ПО СЕРЕДИНЕ торца, а не по кромке.
///
/// Обычная деталь выбирает ближайший детент из трёх — «кромка−», «кромка+»,
/// «центр». На тонком торце все три лежат в пределах 4,5 мм друг от друга, и
/// побеждает тот, к которому ближе курсор: опора садится на край, футорка
/// выходит за пласть. Поэтому опора помечена <c>CentresOnTarget</c> — для неё
/// центр выигрывает у кромок, пока он вообще в пределах порога.
///
/// Габарит бока намеренно асимметричен (16×700×500): на квадратном торце обе оси
/// дают один ответ, и тест был бы зелёным против кода, который их путает.</summary>
public class SnapCoreScrewLegCentreTests : SnapCoreTestBase
{
    private const float SideHalfX = 8f * MM;
    private const float SideBottomY = 150f * MM;

    private const float LegHalfY = 29f * MM;

    private static ElementGeometry Side() =>
        At(Make("Side", new Vector3Int(16, 700, 500)),
            new Vector3(0f, SideBottomY + 350f * MM, 0f));

    private static Box Leg(bool centres) =>
        new Box("Leg", new Vector3Int(25, 58, 25), null, false, centres);

    private static Vector3 LegAt(float x) => new Vector3(x, SideBottomY - LegHalfY, x * 0f);

    [Test]
    public void ALegNudgedTowardsTheEdge_LandsOnTheCentreOfTheThinSide()
    {
        var result = Snap(Leg(true), Side(), LegAt(4f * MM));

        Assert.IsTrue(result.snapped, "торец детали в четырёх миллиметрах — это внутри порога");
        Assert.AreEqual(0f, result.position.x, Tol,
            "опора притянулась к середине торца, а не к его кромке");
    }

    [Test]
    public void AnOrdinaryPartInTheSamePlace_StillPrefersTheNearerEdge()
    {
        var result = Snap(Leg(false), Side(), LegAt(4f * MM));

        Assert.IsTrue(result.snapped, "та же позиция, что и в соседнем тесте: прилипание есть");
        Assert.AreEqual(12.5f * MM - SideHalfX, result.position.x, Tol,
            "без пометки о центровке правило прежнее — ближайший кандидат, здесь выравнивание "
            + "боковой грани опоры по боковой грани детали (0,5 мм против 4 мм до центра). "
            + "Если и эта строка станет нулём, значит центровку включили ВСЕМ, а не опоре");
    }

    [Test]
    public void FarBeyondTheThreshold_TheCentreDoesNotDragTheLegBack()
    {
        var result = Snap(Leg(true), Side(), LegAt(400f * MM));

        Assert.IsFalse(result.snapped,
            "центровка — это детент, а не магнит на всю сцену: за порогом опора свободна");
    }
}
