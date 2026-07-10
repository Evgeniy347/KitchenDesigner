using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Property-based инварианты прилипания (генерация случайных позиций с
/// фиксированным seed). Эта группа ловит регрессии, которые не покрыты
/// точечными сценариями: «снэп никогда не создаёт пересечений», «не меняет
/// поворот», идемпотентность и т.п.
/// </summary>
public class SnapInvariantTests : SnapTestBase
{
    private static Vector3 RandPos(System.Random rng, float range)
    {
        float R() => (float)(rng.NextDouble() * 2.0 - 1.0) * range;
        return new Vector3(R(), R(), R());
    }

    [Test]
    public void Invariant_SnapNeverProducesIntersection()
    {
        var rng = new System.Random(1234);
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);

        int snapped = 0;
        for (int i = 0; i < 300; i++)
        {
            Vector3 p = RandPos(rng, 1.0f);
            var r = Snap(b, a, p);
            if (!r.snapped) continue;
            snapped++;
            b.transform.position = r.position;
            Assert.IsFalse(SnapSystem.ElementsIntersect(b, a),
                $"снэп из {p} дал пересечение в {r.position}");
            b.transform.position = Vector3.zero;
        }
        Assert.Greater(snapped, 0, "часть случайных позиций должна была прилипнуть");
    }

    [Test]
    public void Invariant_SnapPreservesRotation()
    {
        var rng = new System.Random(777);
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero, Quaternion.AngleAxis(90f, Vector3.up));
        var before = b.transform.rotation;

        for (int i = 0; i < 100; i++)
            Snap(b, a, RandPos(rng, 1.0f));

        Assert.AreEqual(before, b.transform.rotation, "снэп не должен менять поворот доски");
    }

    [Test]
    public void Invariant_TrySnapDoesNotMoveBoard()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", new Vector3(0.3f, 0.4f, 0.5f));
        var before = b.transform.position;

        Snap(b, a, new Vector3(0.83f, 0f, 0f)); // TrySnap лишь вычисляет позицию
        Assert.AreEqual(before, b.transform.position,
            "TrySnap должен возвращать moved на исходную позицию");
    }

    [Test]
    public void Invariant_SnapIsIdempotent()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);

        var r1 = Snap(b, a, new Vector3(0.83f, 0f, 0f));
        Assert.IsTrue(r1.snapped);
        var r2 = Snap(b, a, r1.position);
        Assert.IsTrue(r2.snapped, "повторный снэп из снэп-позиции тоже прилипает");
        Assert.AreEqual(r1.position.x, r2.position.x, Tol);
        Assert.AreEqual(r1.position.y, r2.position.y, Tol);
        Assert.AreEqual(r1.position.z, r2.position.z, Tol);
    }

    [Test]
    public void Invariant_SnappedFacesAreFlush()
    {
        // После снэпа грани прилегают: существует контакт с зазором < 0.5 мм.
        // (Контакт может быть и не «face-to-face» — снэп допускает перекрытие от
        // 30%, тогда как face-to-face требует 50%; нам важно само прилегание.)
        var rng = new System.Random(42);
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);

        int checkd = 0;
        for (int i = 0; i < 200 && checkd < 20; i++)
        {
            Vector3 p = RandPos(rng, 1.0f);
            var r = Snap(b, a, p);
            if (!r.snapped) continue;
            b.transform.position = r.position;
            var val = ConstraintValidator.Validate(new List<KitchenElement> { a, b });
            Assert.Greater(val.contacts.Count, 0,
                $"после снэпа из {p} нет прилегающего контакта (поз {r.position})");
            checkd++;
            b.transform.position = Vector3.zero;
        }
        Assert.Greater(checkd, 0);
    }

    [Test]
    public void Invariant_Disabled_NeverSnaps()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        var rng = new System.Random(9);
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero);
        for (int i = 0; i < 50; i++)
            Assert.IsFalse(Snap(b, a, RandPos(rng, 1.0f)).snapped);
    }
}
