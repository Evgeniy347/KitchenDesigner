using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>GrilleElement has no ApplyDimensions override of its own -- WidthMm/HeightMm
/// wrap DimensionsMM directly, so KitchenElement's own DimensionsMM setter already calls
/// ApplyDimensions() (the same base box-scaling every plain board relies on). This still
/// deserves its own guard, the same shape as FenceElementLiveEditTests/
/// DuctElementLiveEditTests: a future refactor that moves Width/Height off DimensionsMM
/// (e.g. to support a real perforated mesh) must not repeat the "field updated, geometry
/// stale" defect those two guard.</summary>
public class GrilleElementLiveEditTests
{
    private const float U = AppConstants.MM_TO_UNITS;
    private readonly List<GameObject> _spawned = new();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private GrilleElement Spawn(int widthMm, int heightMm)
    {
        var go = ElementFactory.CreateGrille(widthMm, heightMm, "G", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<GrilleElement>();
    }

    [Test]
    public void WidthMm_Changed_UpdatesTransformScale()
    {
        var grille = Spawn(150, 150);
        Assert.AreEqual(150f * U, grille.transform.localScale.x, 1e-5f);

        grille.WidthMm = 340;

        Assert.AreEqual(340f * U, grille.transform.localScale.x, 1e-5f,
            "смена WidthMm обязана перестроить масштаб через DimensionsMM/ApplyDimensions()");
    }

    [Test]
    public void HeightMm_Changed_UpdatesTransformScale()
    {
        var grille = Spawn(150, 150);

        grille.HeightMm = 440;

        Assert.AreEqual(440f * U, grille.transform.localScale.y, 1e-5f,
            "смена HeightMm обязана перестроить масштаб через DimensionsMM/ApplyDimensions()");
    }

    [Test]
    public void AirflowM3PerHour_Changed_DoesNotChangeScale()
    {
        var grille = Spawn(150, 150);
        var scaleBefore = grille.transform.localScale;

        grille.AirflowM3PerHour = 500;

        Assert.AreEqual(scaleBefore, grille.transform.localScale,
            "расход воздуха не геометрия — участвует только в VNT-03/ведомости");
    }

    [Test]
    public void SnapToWall_MovesTheGrilleFlushAgainstTheNearestWall_FacingAwayFromIt()
    {
        int wallHeightMm = KitchenSettings.Instance.ConstructionFloorHeightMm;
        float wallCentreY = wallHeightMm * 0.5f * U;
        var wallGo = ElementFactory.CreateWall(new Vector3Int(4000, wallHeightMm, 250), "W",
            new Vector3(0f, wallCentreY, 0f));
        _spawned.Add(wallGo);

        var grille = Spawn(150, 150);
        grille.transform.position = new Vector3(0f, wallCentreY, 2f);

        grille.SnapToWall();

        Assert.Less(grille.transform.position.z, 2f,
            "решётка обязана притянуться к ближайшей стене, а не остаться там, где её создали");
        Assert.Greater(grille.transform.position.z, 0f,
            "решётка садится НА поверхность стены (со стороны, где её поставили), не сквозь неё");
    }
}
