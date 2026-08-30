using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ворота «кромочный кандидат не рвёт уже существующее выравнивание»
/// умеют только ОТКАЗАТЬСЯ от кандидата в пользу подтверждённого контакта.
/// Подтверждённого контакта может не быть вовсе — тогда отказ превращает
/// сработавший снэп в отсутствие снэпа, и деталь перестаёт липнуть совсем.
///
/// Так и вышло: свип `SnapMutationTests` по живой сцене выдал 19 ошибок
/// MOVE-NOSNAP на деталях, которые до правки прилипали. Все они — про детали,
/// вошедшие ВНУТРЬ соседа: там со-направленное выравнивание есть, а нулевого
/// встречного контакта нет. Сцена ниже — минимальная такая: деталь стоит на дне
/// короба (низ заподлицо с низом → выравнивание по −Y), встречных контактов
/// в пределах порога у неё нет, а сверху ждёт полка, делящая с ней ровно ребро.</summary>
public class SnapCoreAlignmentGateTests : SnapCoreTestBase
{
    private const float BoxHalf = 500f * MM;
    private const float PartTopY = -400f * MM;
    private const float ShelfBottomY = -390f * MM;

    private static ElementGeometry CabinetBox() =>
        At(Make("Box", new Vector3Int(1000, 1000, 1000)), Vector3.zero);

    private static ElementGeometry Shelf() =>
        At(Make("Shelf", new Vector3Int(300, 18, 300)),
            new Vector3(200f * MM, ShelfBottomY + 9f * MM, 0f));

    private static Box Part() => Make("Part", new Vector3Int(100, 100, 100));

    private static Vector3 SeatedOnBoxFloor => new Vector3(0f, -BoxHalf + 50f * MM, 0f);

    [Test]
    public void PartInsideBox_EdgeCandidateBreaksAlignment_StillSnaps_NotNothing()
    {
        var scene = new List<ElementGeometry> { CabinetBox(), Shelf() };

        var r = Snap(Part(), scene, SeatedOnBoxFloor);

        Assert.IsTrue(r.snapped,
            "отказ от кромочного кандидата не имеет права оставить деталь совсем без снэпа: "
            + "подтверждённого контакта здесь нет, откатываться некуда");
        Assert.AreEqual(ShelfBottomY - 50f * MM, r.position.y, Tol,
            "верх детали встаёт под полку — тот самый кандидат, который ворота отбрасывали");
    }

    [Test]
    public void PartInsideBox_BottomFlushWithBoxFloor_IsAnAlignmentButNotAContact()
    {
        var r = Snap(Part(), CabinetBox(), SeatedOnBoxFloor);

        Assert.IsFalse(r.snapped,
            "контроль к паре выше: со-направленное выравнивание по −Y есть, а встречного "
            + "контакта нет — значит в первом тесте откатываться действительно некуда");
    }
}
