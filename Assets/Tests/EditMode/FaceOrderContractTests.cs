using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Порядок граней в <see cref="KitchenElement.GetFaces"/> — КОНТРАКТ,
/// а не деталь реализации: index/2 = ось (0=X, 1=Y, 2=Z), чётный индекс =
/// положительное направление. Из индекса выводит ось <c>ResizeHandleManager</c>,
/// на нём же стоят диагностика снэпа (<c>movedFaceIndex / 2</c>) и подписи
/// граней в мутирующем тесте.
///
/// Тест живёт в EditMode, а не в ядре: он проверяет ЭЛЕМЕНТ СЦЕНЫ, тогда как
/// сам тип Face переехал в KitchenDesigner.Geometry.</summary>
public class FaceOrderContractTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement MakeBoard(Vector3Int dims, Quaternion rot)
    {
        var go = new GameObject("contract-board");
        _spawned.Add(go);
        go.transform.rotation = rot;
        var el = go.AddComponent<KitchenElement>();
        el.PartName = "contract-board";
        el.DimensionsMM = dims;
        return el;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void GetFaces_ReturnsExactlySixFaces()
    {
        var el = MakeBoard(new Vector3Int(600, 400, 18), Quaternion.identity);
        Assert.AreEqual(6, el.GetFaces().Length);
    }

    /// <summary>Чётный индекс смотрит вдоль своей оси, нечётный — против.</summary>
    [Test]
    public void FaceIndex_EncodesAxisAndDirection()
    {
        var el = MakeBoard(new Vector3Int(600, 400, 18), Quaternion.identity);
        var faces = el.GetFaces();

        var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        for (int i = 0; i < 6; i++)
        {
            var expected = (i % 2 == 0) ? axes[i / 2] : -axes[i / 2];
            Assert.AreEqual(expected.x, faces[i].normal.x, 1e-4f, $"грань {i}: X нормали");
            Assert.AreEqual(expected.y, faces[i].normal.y, 1e-4f, $"грань {i}: Y нормали");
            Assert.AreEqual(expected.z, faces[i].normal.z, 1e-4f, $"грань {i}: Z нормали");
        }
    }

    /// <summary>Пара граней одной оси лежит по разные стороны от центра на
    /// половине габарита — иначе ресайз от индекса поедет.</summary>
    [Test]
    public void FacePairs_AreOppositeAndHalfSizeApart()
    {
        var dims = new Vector3Int(600, 400, 18);
        var el = MakeBoard(dims, Quaternion.identity);
        var faces = el.GetFaces();
        var half = new[]
        {
            dims.x * AppConstants.MM_TO_UNITS * 0.5f,
            dims.y * AppConstants.MM_TO_UNITS * 0.5f,
            dims.z * AppConstants.MM_TO_UNITS * 0.5f,
        };

        for (int axis = 0; axis < 3; axis++)
        {
            var plus = faces[axis * 2];
            var minus = faces[axis * 2 + 1];

            Assert.AreEqual(-1f, Vector3.Dot(plus.normal, minus.normal), 1e-4f,
                $"ось {axis}: грани пары обязаны смотреть навстречу");
            Assert.AreEqual(2f * half[axis], Vector3.Distance(plus.center, minus.center), 1e-4f,
                $"ось {axis}: расстояние между гранями пары = габарит");
        }
    }

    /// <summary>Оси грани перпендикулярны её нормали и друг другу: на этом стоит
    /// проекция footprint'а в прямоугольник (GetFaceRect / RectFor).</summary>
    [Test]
    public void FaceAxes_AreOrthonormalToNormal()
    {
        var el = MakeBoard(new Vector3Int(600, 400, 18), Quaternion.identity);

        foreach (var f in el.GetFaces())
        {
            Assert.AreEqual(0f, Vector3.Dot(f.normal, f.rightAxis), 1e-4f, "rightAxis ⊥ normal");
            Assert.AreEqual(0f, Vector3.Dot(f.normal, f.upAxis), 1e-4f, "upAxis ⊥ normal");
            Assert.AreEqual(0f, Vector3.Dot(f.rightAxis, f.upAxis), 1e-4f, "rightAxis ⊥ upAxis");
            Assert.AreEqual(1f, f.rightAxis.magnitude, 1e-4f, "rightAxis единичная");
            Assert.AreEqual(1f, f.upAxis.magnitude, 1e-4f, "upAxis единичная");
        }
    }

    /// <summary>У повёрнутой детали контракт держится в ЛОКАЛЬНЫХ осях: индекс
    /// по-прежнему задаёт ось детали, просто ось теперь повёрнута.</summary>
    [Test]
    public void Contract_SurvivesRotation()
    {
        var rot = ManagedRotation.Euler(0f, 90f, 0f);
        var el = MakeBoard(new Vector3Int(600, 400, 18), rot);
        var faces = el.GetFaces();

        var axes = new[] { rot * Vector3.right, rot * Vector3.up, rot * Vector3.forward };
        for (int i = 0; i < 6; i++)
        {
            var expected = (i % 2 == 0) ? axes[i / 2] : -axes[i / 2];
            Assert.AreEqual(1f, Vector3.Dot(expected, faces[i].normal), 1e-3f,
                $"грань {i}: нормаль обязана следовать за поворотом детали");
        }
    }

    /// <summary>Размер грани — это габариты ДВУХ ДРУГИХ осей.</summary>
    [Test]
    public void FaceSize_MatchesTheOtherTwoDimensions()
    {
        var dims = new Vector3Int(600, 400, 18);
        var el = MakeBoard(dims, Quaternion.identity);
        var faces = el.GetFaces();
        float u = AppConstants.MM_TO_UNITS;

        Assert.AreEqual(dims.y * u, faces[0].size.x, 1e-4f, "грань X: ширина = Y");
        Assert.AreEqual(dims.z * u, faces[0].size.y, 1e-4f, "грань X: высота = Z");
        Assert.AreEqual(dims.x * u, faces[2].size.x, 1e-4f, "грань Y: ширина = X");
        Assert.AreEqual(dims.z * u, faces[2].size.y, 1e-4f, "грань Y: высота = Z");
        Assert.AreEqual(dims.x * u, faces[4].size.x, 1e-4f, "грань Z: ширина = X");
        Assert.AreEqual(dims.y * u, faces[4].size.y, 1e-4f, "грань Z: высота = Y");
    }
}
