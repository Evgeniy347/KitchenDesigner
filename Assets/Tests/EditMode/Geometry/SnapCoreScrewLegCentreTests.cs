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
        new Box("Leg", new Vector3Int(25, 58, 25), null, false,
            centres ? Vector3.up : Vector3.zero);

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

    // ─ Живой случай из сцены: Vintovaya_opora под A4_plint_drawer_L_inner ───
    //
    // Цоколь 482x80x16 висит дном на 20 мм над полом, опора стоит пяткой на полу
    // и ростом 58 мм перекрывает его по высоте на 38 мм. Раньше побеждали БОКОВЫЕ
    // контакты: опора приклеивалась то к задней пласти цоколя, то к передней (в
    // сцене — прыжок с −1856 сразу на −1814 мимо середины −1835), а кандидат «под
    // деталью, по центру» отбрасывался как ломающий уже зафиксированную ось Y —
    // ту самую, которой пятка стоит на полу.

    private const float PlinthBottomY = 20f * MM;
    private const float PlinthHalfZ = 8f * MM;

    private const float LegCentreY = 29f * MM;

    private static ElementGeometry Plinth() =>
        At(Make("Plinth", new Vector3Int(482, 80, 16)),
            new Vector3(0f, PlinthBottomY + 40f * MM, 0f));

    private static Box FloorLeg() =>
        new Box("Leg", new Vector3Int(25, 58, 25), null, false, Vector3.up);

    private static List<ElementGeometry> PlinthScene() =>
        new List<ElementGeometry> { Floor(), Plinth() };

    private static Vector3 LegOnFloorAt(float z) => new Vector3(0f, LegCentreY, z);

    [Test]
    public void UnderThePlinth_TheLegCentresOnItsThickness()
    {
        var result = Snap(FloorLeg(), PlinthScene(), LegOnFloorAt(-12f * MM));

        Assert.IsTrue(result.snapped, "подошла под цоколь — середина обязана предлагаться");
        Assert.AreEqual(0f, result.position.z, Tol,
            "опора встаёт на середину шестнадцатимиллиметровой толщины цоколя");
    }

    [Test]
    public void Centring_LeavesTheHeightAlone_TheFootStaysOnTheFloor()
    {
        var result = Snap(FloorLeg(), PlinthScene(), LegOnFloorAt(-12f * MM));

        Assert.AreEqual(LegCentreY, result.position.y, Tol,
            "высоту опора добирает длиной резьбы уже после отпускания, а не съезжая "
            + "вниз при перетаскивании: сдвиг вдоль оси крепления у центровки нулевой, "
            + "иначе кандидат ломает ось, которой пятка стоит на полу, и его выбрасывают");
    }

    [Test]
    public void FlushBehindThePlinth_TheLegGoesToTheMiddle_NotToTheFarSide()
    {
        var result = Snap(FloorLeg(), PlinthScene(), LegOnFloorAt(-20.5f * MM));

        Assert.IsTrue(result.snapped, "цоколь в пределах порога — предложение обязано быть");
        Assert.AreEqual(0f, result.position.z, Tol,
            "именно этот случай и был в сцене: стоя вплотную ЗА цоколем, опора уезжала "
            + "на 41 мм к его передней пласти (−1856 → −1814) мимо середины. Боковых "
            + "контактов у круглой пятки Ø25 не бывает — есть только «под деталью, "
            + "по центру» (−1835)");
    }

    [Test]
    public void TheFarSideOfThePlinth_IsNotOfferedEither()
    {
        var result = Snap(FloorLeg(), PlinthScene(), LegOnFloorAt(14f * MM));

        Assert.IsTrue(result.snapped, "с той стороны середина видна ровно так же");
        Assert.AreEqual(0f, result.position.z, Tol,
            "подходя с другой стороны, опора приходит на ту же середину");
    }

    // ─ Почему брутфорс SnapMutationTests молчит про эти две грани ───────────
    //
    // Перебор пар граней требует одного: встречные нормали, зазор в пределах
    // порога, перекрытие не меньше 30% — значит обязано прилипнуть. Для опоры
    // это требование неверно дважды, и оба раза оно давало NO-SNAP на здоровом
    // коде. Тесты ниже говорят, что верно ВМЕСТО него, и краснеют, если отбор
    // снова начнёт брать эти грани.

    private static ElementGeometry BoardBesideTheLeg() =>
        At(Make("Board", new Vector3Int(482, 80, 16)), new Vector3(0f, 60f * MM, 0f));

    [Test]
    public void ASideFaceOfTheLeg_IsNotOfferedEvenAtFullOverlap()
    {
        var result = Snap(FloorLeg(), BoardBesideTheLeg(), new Vector3(0f, LegCentreY, -52.5f * MM));

        Assert.IsFalse(result.snapped,
            "бок пятки Ø25 стоит в 32 мм от пласти доски и перекрывает её на 66% — "
            + "по правилу «встречные грани в пределах порога» опора обязана была бы прилипнуть. "
            + "У круглой пятки боковых контактов не бывает: в отборе участвуют только грани оси "
            + "крепления, иначе бок выигрывает по сдвигу у посадки и уносит опору с середины "
            + "царги на её пласть. Сними это правило — и здесь появится снэп, а брутфорс "
            + "перестанет отличать его от исправного");
    }

    [Test]
    public void LoweredUnderThePlinth_TheLegIsNotPulledBackUpAlongItsThread()
    {
        var testPos = new Vector3(0f, LegCentreY - 7f * MM, -5f * MM);
        var result = Snap(FloorLeg(), Plinth(), testPos);

        Assert.IsTrue(result.snapped,
            "по толщине цоколя посадка есть: опора смещена с середины на 5 мм");
        Assert.AreEqual(0f, result.position.z, Tol,
            "и в плоскости крепления посадка возвращает её на середину");
        Assert.AreEqual(testPos.y, result.position.y, Tol,
            "а вдоль оси крепления не двигает вовсе: опущенную на 7 мм опору снэп обратно к "
            + "пласти цоколя не тянет — высоту она добирает длиной резьбы уже после отпускания. "
            + "Именно этого требовал брутфорс строкой «Vintovaya_opora_1[f2]↔A4_side_L[f3] "
            + "gap=42,0mm ovl=100% away=7mm», и требовал напрасно");
    }

    // ─ На чём стоят два допущения оракула свипа ─────────────────────────────
    //
    // SnapMutationTests больше не зовёт INTERSECT-AFTER-SNAP на посадке
    // (SeatedIntoIt) и не считает грань крепления участником спора за ось
    // (MOVE-COMPETITION): прогон 2026-09-08 дал 565 и 78 таких строк, и все они
    // были про три винтовые опоры под цоколем. Оба допущения держатся на двух
    // фактах ниже. Сломай любой — и красным станет здесь, а не тихо нигде.

    [Test]
    public void TheSeatedLeg_ThreadsIntoThePlinth_AndThatOverlapIsTheSeating()
    {
        var result = Snap(FloorLeg(), PlinthScene(), LegOnFloorAt(-12f * MM));

        Assert.IsTrue(result.snapped, "опора подошла под цоколь — посадка обязана быть");

        float legTopY = result.position.y + 29f * MM;
        Assert.Greater(legTopY, PlinthBottomY + Tol,
            "резьба ушла В цоколь: коробки опоры и цоколя ПЕРЕСЕКАЮТСЯ, и это и есть "
            + "посадка, а не столкновение. Свип освобождён от проверки пересечения ровно "
            + "на этот случай; перестанет посадка заводить опору внутрь — освобождение "
            + "станет прикрытием, и красной обязана быть эта строка");
        Assert.Less(legTopY, PlinthBottomY + 80f * MM - Tol,
            "но насквозь не проходит: 58 мм роста от пола против дна цоколя на 20 мм и "
            + "его же верха на 100 мм");
    }

    [Test]
    public void LiftedOffTheFloor_TheLegComesBackDown_TheCarcassDoesNotTakeIt()
    {
        var result = Snap(FloorLeg(), PlinthScene(),
            new Vector3(0f, LegCentreY + 24f * MM, 0f));

        Assert.IsTrue(result.snapped, "пол в 24 мм под пяткой — это внутри порога");
        Assert.AreEqual(LegCentreY, result.position.y, Tol,
            "поднятая опора возвращается пяткой на ПОЛ. Свип звал это ошибкой выбора — "
            + "«выбран Pol_1 gap=24,0мм, но ближе A4_side_L gap=18,0мм» — сравнивая две "
            + "разные величины: у грани крепления зазор по плоскости не значит ничего, "
            + "вдоль оси крепления посадка не двигает деталь вовсе, и единственный "
            + "кандидат, который вообще меняет высоту, — пол");
        Assert.AreEqual(0f, result.position.z, Tol,
            "а поперёк её не уносит: она уже на середине толщины цоколя");
    }
}
