using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Инвариант миллиметровой сетки: на целых мм стоят ГРАНИ, а не центр.
/// У детали нечётного габарита центр обязан лежать на половине — это норма.</summary>
public class MmGridTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement Make(Vector3Int dims, Vector3 pos, Quaternion? rot = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = pos;
        if (rot.HasValue) go.transform.rotation = rot.Value;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = "Деталь";
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    /// <summary>Мировые границы детали в миллиметрах.</summary>
    private static (Vector3 min, Vector3 max) BoundsMm(KitchenElement e)
    {
        var verts = e.GetVertices();
        Vector3 min = verts[0], max = verts[0];
        foreach (var v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        float toMm = 1f / AppConstants.MM_TO_UNITS;
        return (min * toMm, max * toMm);
    }

    [Test]
    public void EvenDims_HalfMillimetreCenter_IsSnapped()
    {
        // Боковина 16 мм с центром на 1200.5 стоит гранями на 1192.5 и 1208.5 —
        // ровно случай Cab_L_Side_L из реального проекта.
        var e = Make(new Vector3Int(600, 2700, 16), new Vector3(0f, 1.35f, 1.2005f));
        Assert.IsTrue(MmGrid.Snap(e), "деталь стояла мимо сетки — должна сдвинуться");

        var (min, max) = BoundsMm(e);
        Assert.AreEqual(1193f, min.z, 0.01f);
        Assert.AreEqual(1209f, max.z, 0.01f);
    }

    [Test]
    public void OddDims_HalfMillimetreCenter_IsAlreadyOnGrid()
    {
        // Доска 1967 мм гранями на 1208 и 3175 → центр 2191.5. Половина в ЦЕНТРЕ
        // законна, трогать деталь нельзя.
        var e = Make(new Vector3Int(1967, 70, 16), new Vector3(2.1915f, 0.035f, 0f));
        var before = e.transform.position;

        Assert.IsFalse(MmGrid.Snap(e), "деталь уже на сетке");
        Assert.AreEqual(before, e.transform.position);

        var (min, max) = BoundsMm(e);
        Assert.AreEqual(1208f, min.x, 0.01f);
        Assert.AreEqual(3175f, max.x, 0.01f);
    }

    [Test]
    public void Rotated90_IsSnappedAlongWorldAxes()
    {
        var e = Make(new Vector3Int(1000, 100, 18), new Vector3(0f, 0f, 0f),
            ManagedRotation.Euler(0f, 90f, 0f));
        e.transform.position = new Vector3(0.0003f, 0.0004f, 0.0007f);
        MmGrid.Snap(e);

        var (min, _) = BoundsMm(e);
        foreach (var coord in new[] { min.x, min.y, min.z })
            Assert.AreEqual(Mathf.Round(coord), coord, 0.01f, "грань должна лечь на целый мм");
    }

    [Test]
    public void RotatedOffAxis_IsLeftAlone()
    {
        // У детали, повёрнутой на 37°, «грань на мм-сетке» смысла не имеет.
        var e = Make(new Vector3Int(600, 100, 18), new Vector3(0.1234f, 0f, 0.5678f),
            ManagedRotation.Euler(0f, 37f, 0f));
        var before = e.transform.position;

        Assert.IsFalse(MmGrid.Snap(e));
        Assert.AreEqual(before, e.transform.position);
    }

    [Test]
    public void RoundMm_HalfGoesUp_NotToEven()
    {
        // Mathf.Round округляет половину к чётному: 1208.5 → 1208, а 1207.5 → 1208.
        // Для стыков это недопустимо — одинаковые .5 разъезжаются в разные стороны.
        Assert.AreEqual(1209f, MmGrid.RoundMm(1208.5f));
        Assert.AreEqual(1208f, MmGrid.RoundMm(1207.5f));
        Assert.AreEqual(1208f, MmGrid.RoundMm(1208.4f));
        Assert.AreEqual(-1208f, MmGrid.RoundMm(-1208.5f));

        // Ровной половины во float32 не бывает: обе стороны границы обязаны
        // округлиться одинаково, иначе один и тот же дефект разъезжается.
        Assert.AreEqual(1193f, MmGrid.RoundMm(1192.49995f));
        Assert.AreEqual(1193f, MmGrid.RoundMm(1192.50004f));
    }

    [Test]
    public void SnapPosition_DoesNotMoveElement()
    {
        var e = Make(new Vector3Int(600, 2700, 16), new Vector3(0f, 1.35f, 0f));
        var before = e.transform.position;

        var snapped = MmGrid.SnapPosition(e, new Vector3(0f, 1.35f, 1.2005f));

        Assert.AreEqual(before, e.transform.position, "примерка не должна двигать деталь");
        Assert.AreEqual(1.2010f, snapped.z, 1e-5f, "0.5 мм вверх → грани на 1193/1209");
    }

    [Test]
    public void LoweredWall_IsLeftAlone_SoTheGridDoesNotSinkIt()
    {
        var go = ElementFactory.CreateWall(
            new Vector3Int(100, 2700, 3000), "GridWall", new Vector3(0.0002f, 1.35f, 0f));
        _spawned.Add(go);
        var element = go.GetComponent<KitchenElement>();
        var wall = go.GetComponent<Wall>();

        Assert.IsTrue(MmGrid.Snap(element),
            "стена в полный рост мимо сетки выравнивается — положительный контроль");

        go.transform.position = new Vector3(0.0002f, 1.35f, 0f);
        wall.SetLowered(true, 0.1f);
        var before = go.transform.position;

        Assert.IsFalse(MmGrid.Snap(element),
            "у опущенной стены вершины считаются от ПОЛНОЙ геометрии, и правка позиции "
            + "по ним увела бы стену вниз вместе с опусканием");
        Assert.AreEqual(before, go.transform.position, "позицию не тронули");
    }
}
