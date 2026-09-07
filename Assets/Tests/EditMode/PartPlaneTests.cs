using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Оси детали-хозяина под врезной техникой. Раньше эта арифметика жила ДВАЖДЫ —
/// своими копиями в мойке и в варочной (UpAxisOf / PlaneAxes / AxisVector /
/// LocalPose / BaseRotationOn / TwistAngle); теперь она одна, и знание, которое
/// раньше лежало комментариями над этими методами, проверяется здесь.
/// </summary>
public class PartPlaneTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            var el = go.GetComponent<KitchenElement>();
            if (el != null) PartRegistry.Unregister(el);
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    private KitchenElement Part(Vector3Int dimensionsMM, Quaternion rotation)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _spawned.Add(go);
        var el = go.AddComponent<KitchenElement>();
        el.PartName = "Part" + _spawned.Count;
        el.transform.rotation = rotation;
        el.DimensionsMM = dimensionsMM;
        return el;
    }

    [Test]
    public void Of_BoardLaidFlatByRotation_FindsThicknessAlongItsLocalZ()
    {
        var top = Part(new Vector3Int(1200, 600, 38), ManagedRotation.Euler(-90f, 0f, 0f));
        var plane = PartPlane.Of(top);

        Assert.AreEqual(2, plane.UpAxis,
            "столешницу собирают повёрнутой доской: толщина лежит по ЛОКАЛЬНОЙ Z, "
            + "и верхнюю грань приходится искать перебором осей, а не считать, что это Y");
        Assert.AreEqual(38, plane.ThicknessMM);
        Assert.AreEqual(1200, plane.SizeAlongA);
        Assert.AreEqual(600, plane.SizeAlongB);
    }

    [Test]
    public void Of_BoxWithThicknessAlongY_FindsThatAxisInstead()
    {
        var top = Part(new Vector3Int(1200, 38, 600), Quaternion.identity);
        var plane = PartPlane.Of(top);

        Assert.AreEqual(1, plane.UpAxis, "короб: толщина по Y — верхняя грань та же самая");
        Assert.AreEqual(38, plane.ThicknessMM);
        Assert.AreEqual(1200, plane.SizeAlongA);
        Assert.AreEqual(600, plane.SizeAlongB);
    }

    [Test]
    public void Of_UpsideDownBoard_KeepsTheAxisButFlipsTheSign()
    {
        var top = Part(new Vector3Int(1200, 600, 38), ManagedRotation.Euler(90f, 0f, 0f));
        var plane = PartPlane.Of(top);

        Assert.AreEqual(2, plane.UpAxis);
        Assert.AreEqual(-1f, plane.UpSign,
            "перевёрнутая доска: та же ось, но «вверх» смотрит в минус — иначе врезка "
            + "ушла бы под деталь");
        Assert.Less(Vector3.Distance(plane.UpWorld, Vector3.up), 1e-3f);
    }

    [Test]
    public void MeshAxes_FollowTheOrderGrooveMeshExpects()
    {
        Assert.AreEqual((0, 1), AxesOf(new Vector3Int(1200, 600, 38), ManagedRotation.Euler(-90f, 0f, 0f)),
            "вырез вдоль Z — сетка в XY, канонический случай GrooveMesh.Build");
        Assert.AreEqual((0, 2), AxesOf(new Vector3Int(1200, 38, 600), Quaternion.identity),
            "вдоль Y — сетка в XZ");
        Assert.AreEqual((2, 1), AxesOf(new Vector3Int(38, 600, 1200), ManagedRotation.Euler(0f, 0f, -90f)),
            "вдоль X — сетка в ZY");
    }

    private (int a, int b) AxesOf(Vector3Int dims, Quaternion rot)
    {
        var plane = PartPlane.Of(Part(dims, rot));
        return (plane.AxisA, plane.AxisB);
    }

    [Test]
    public void IsHorizontal_TiltedPart_IsRejected()
    {
        Assert.IsTrue(PartPlane.Of(Part(new Vector3Int(1200, 38, 600), Quaternion.identity)).IsHorizontal);
        Assert.IsFalse(
            PartPlane.Of(Part(new Vector3Int(1200, 38, 600), ManagedRotation.Euler(0f, 0f, 45f))).IsHorizontal,
            "наклонённая деталь — не столешница: ни одна её ось не смотрит вверх достаточно "
            + "уверенно, и врезать в неё технику нельзя");
    }

    [Test]
    public void PoseOf_PointAboveTheSurface_ReportsHeightFromTheTopFace()
    {
        var top = Part(new Vector3Int(1200, 38, 600), Quaternion.identity);
        var plane = PartPlane.Of(top);
        float halfThickness = 19f * AppConstants.MM_TO_UNITS;

        var (offX, offY, heightMM) = plane.PoseOf(
            top.transform.position + new Vector3(0.1f, halfThickness + 0.05f, -0.2f));

        Assert.AreEqual(100, offX);
        Assert.AreEqual(-200, offY);
        Assert.AreEqual(50f, heightMM, 0.5f, "высота меряется от ПЛАСТИ, а не от центра детали");
    }

    [Test]
    public void SurfacePoint_IsTheInverseOfPoseOf()
    {
        var top = Part(new Vector3Int(1200, 600, 38), ManagedRotation.Euler(-90f, 30f, 0f));
        var plane = PartPlane.Of(top);

        var world = plane.SurfacePoint(150, -80);
        var (offX, offY, heightMM) = plane.PoseOf(world);

        Assert.AreEqual(150, offX);
        Assert.AreEqual(-80, offY);
        Assert.AreEqual(0f, heightMM, 0.5f, "точка на пласти лежит на нулевой высоте");
    }

    [Test]
    public void TwistAroundUp_TiltIsDiscarded_OnlyRotationAboutTheNormalCounts()
    {
        var top = Part(new Vector3Int(1200, 38, 600), Quaternion.identity);
        var plane = PartPlane.Of(top);

        var tiltOnly = ManagedRotation.RotX(20f);
        Assert.AreEqual(0f, plane.TwistAroundUpDeg(tiltOnly), 0.01f,
            "наклон панель не разворачивает: AlignToPart тут же уложит её обратно в пласть");

        var twistAndTilt = ManagedRotation.RotY(35f) * tiltOnly;
        Assert.AreEqual(35f, plane.TwistAroundUpDeg(twistAndTilt), 0.5f,
            "из смешанного поворота берётся только компонента вокруг нормали детали");
    }

    [Test]
    public void TwistAngle_HalfTurnAcrossTheAxis_DegeneratesToZero()
    {
        var delta = new Quaternion(1f, 0f, 0f, 0f);
        float twist = PartPlane.TwistAngleDeg(delta, Vector3.up);
        Assert.IsFalse(float.IsNaN(twist), "нормировка нулевого кватерниона даёт NaN — он поедет в YawDeg");
        Assert.AreEqual(0f, twist, 0.01f,
            "поворот ровно на 180° ПОПЕРЁК оси: твист вырождается, и разложение "
            + "swing-twist обязано вернуть ноль, а не NaN от нормировки нулевого кватерниона");
    }

    [Test]
    public void TwistAngle_IsSigned()
    {
        Assert.AreEqual(-40f,
            PartPlane.TwistAngleDeg(ManagedRotation.RotY(-40f), Vector3.up), 0.5f,
            "знак нужен: иначе разворот копился бы только в одну сторону");
    }
}
