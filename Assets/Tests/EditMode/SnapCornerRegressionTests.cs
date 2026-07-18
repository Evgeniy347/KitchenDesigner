using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Регрессии «прилипло, но красное»: раньше TrySnap применял РОВНО ОДИН
/// кандидат, поэтому в углу (бок соседа + стена) вторая ось оставалась с
/// зазором или невидимым проникновением — деталь снэпилась и тут же краснела,
/// а микросдвиг мыши (другой кандидат из соседней ячейки сетки) «магически»
/// чинил её. Теперь снэп добирает ортогональные кандидаты, а конфликтное
/// кромочное выравнивание (du/dv) обнуляется покомпонентно, не хороня
/// кандидат целиком.
/// </summary>
public class SnapCornerRegressionTests : SnapTestBase
{
    // Угол: куб A (бок) + стена W (сзади). B подносится в угол.
    private KitchenElement? _a;
    private KitchenElement? _wall;

    private List<KitchenElement> BuildCorner()
    {
        _a = Make("A", new Vector3Int(600, 600, 600), Vector3.zero);          // +X грань на 0.3
        _wall = Make("W", new Vector3Int(2000, 600, 100), new Vector3(0.5f, 0f, 0.4f)); // -Z грань на 0.35
        return new List<KitchenElement> { _a, _wall };
    }

    [Test]
    public void Corner_SnapsBothAxes_FlushToSideAndWall()
    {
        var others = BuildCorner();
        var b = Make("B", new Vector3Int(600, 600, 600), Vector3.zero);

        // 30 мм до A по X и 30 мм до стены по Z — оба в пределах порога 50 мм.
        var r = Snap(b, others, new Vector3(0.63f, 0f, 0.02f));

        Assert.IsTrue(r.snapped, "в углу деталь должна прилипнуть");
        Assert.AreEqual(0.6f, r.position.x, Tol, "заподлицо с боком A (одна ось)");
        Assert.AreEqual(0.05f, r.position.z, Tol, "И заподлицо со стеной (вторая ось)");
        Assert.AreEqual(0f, r.position.y, Tol);

        b.transform.position = r.position;
        Assert.IsFalse(SnapSystem.ElementsIntersect(b, _a!), "нет пересечения с A");
        Assert.IsFalse(SnapSystem.ElementsIntersect(b, _wall!), "нет пересечения со стеной");
    }

    [Test]
    public void Corner_PenetratingWall_SnapPullsOut_NoRedState()
    {
        // Репро «прилипло, но красное»: тестовая позиция на 20 мм ВНУТРИ стены,
        // а ближайший кандидат — бок A. Раньше применялся только он, и деталь
        // оставалась в стене (невидимое пересечение → красная подсветка).
        var others = BuildCorner();
        var b = Make("B", new Vector3Int(600, 600, 600), Vector3.zero);

        var r = Snap(b, others, new Vector3(0.61f, 0f, 0.07f));

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.6f, r.position.x, Tol, "заподлицо с боком A");
        Assert.AreEqual(0.05f, r.position.z, Tol, "вытолкнута из стены заподлицо");

        b.transform.position = r.position;
        Assert.IsFalse(SnapSystem.ElementsIntersect(b, _wall!),
            "после снэпа деталь не должна оставаться в стене");
        var val = ConstraintValidator.Validate(new List<KitchenElement> { _a!, _wall!, b });
        Assert.IsFalse(val.violations.Contains(b),
            "снэпнутая деталь не должна быть нарушением (красной)");
    }

    [Test]
    public void EdgeAlignConflict_DropsComponent_InsteadOfRejectingSnap()
    {
        // Деталь на полу липнет к соседу, чья нижняя кромка на 30 мм выше:
        // кромочное выравнивание по Y рвало бы контакт с полом. Раньше кандидат
        // отбрасывался ЦЕЛИКОМ (снэп «иногда не срабатывает»), теперь
        // обнуляется только вертикальная компонента: деталь прилипает по Z,
        // оставаясь на полу.
        MakeFloor();
        var moved = MakeStd("M", new Vector3(0.5f, 0.209f, 0.3f)); // на полу
        var t = MakeStd("T", new Vector3(0.5f, 0.239f, 0.34f));    // низ на 30 мм выше пола

        var others = new List<KitchenElement>(_spawned.ConvertAll(
            go => go.GetComponent<KitchenElement>()));
        var r = SnapSystem.TrySnap(moved, others, moved.transform.position);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("T", r.targetName, "цель — сосед, а не подтверждение пола");
        Assert.AreEqual(0.322f, r.position.z, Tol, "заподлицо с гранью T (0.331-0.009)");
        Assert.AreEqual(0.209f, r.position.y, Tol, "деталь осталась на полу");
    }
}
