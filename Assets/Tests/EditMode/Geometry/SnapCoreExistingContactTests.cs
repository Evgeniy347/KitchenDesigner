using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Существующий контакт против нового снэпа — на снимках.
///
/// Баг «в конкретных позициях не прилипает»: кандидат с нулевым сдвигом (деталь
/// УЖЕ заподлицо с соседом или полом) побеждал содержательные снэпы, и деталь,
/// скользящая по грани соседа, никогда не прилипала к стене. Правило после
/// фикса: содержательный снэп предпочтительнее подтверждения текущего контакта,
/// но не должен этот контакт РАЗРЫВАТЬ.
///
/// Перенос сценового `SnapExistingContactTests` (сцена снята с реального
/// проекта пользователя, 2026-07-04).</summary>
public class SnapCoreExistingContactTests : SnapCoreTestBase
{
    /// <summary>Стоящая деталь, к грани которой прижата лежащая.</summary>
    private static ElementGeometry Standing() =>
        At(Make("Standing", new Vector3Int(600, 720, 19), Euler(0f, 90f, 180f)),
            new Vector3(-0.8905f, 0.449476f, 0.7418f));

    private static Box Lying() =>
        Make("Lying", new Vector3Int(600, 720, 19), Euler(270f, 0f, 0f));

    private static ElementGeometry Wall() =>
        At(Make("Wall", new Vector3Int(2800, 2500, 100)), new Vector3(-0.1f, 1.25f, 1.45f));

    private static List<ElementGeometry> UserScene() =>
        new List<ElementGeometry> { Floor(), Standing(), Wall() };

    [Test]
    public void LyingBoard_SlidingAlongNeighbor_SnapsToWall()
    {
        // Торец лежащей заподлицо с гранью стоящей (x = −0.9) — этот контакт
        // сохраняется при движении по Z. Задняя кромка на z+0.36, стена на 1.40.
        var testPos = new Vector3(-1.2f, 0.098976f, 1.03f); // зазор до стены 10 мм
        var r = Snap(Lying(), UserScene(), testPos);

        Assert.IsTrue(r.snapped, "должна прилипнуть к стене");
        Assert.AreEqual("Wall", r.targetName,
            "цель — стена (контакт со стоящей деталью не должен её маскировать)");
        Assert.AreEqual(1.04f, r.position.z, Tol, "прижатие задней кромки к стене (1.40 − 0.36)");
        Assert.AreEqual(testPos.x, r.position.x, Tol, "сдвиг только по Z");
        Assert.AreEqual(testPos.y, r.position.y, Tol, "сдвиг только по Z");
    }

    [Test]
    public void LyingBoard_WholeUserRange_SnapsToWall()
    {
        // Диапазон из бага: z от 1.026 до ~1.039.
        foreach (float z in new[] { 1.026f, 1.030f, 1.035f, 1.039f })
        {
            var r = Snap(Lying(), UserScene(), new Vector3(-1.2f, 0.098976f, z));
            Assert.IsTrue(r.snapped, $"z={z}: должна прилипнуть");
            Assert.AreEqual("Wall", r.targetName, $"z={z}: цель — стена");
            Assert.AreEqual(1.04f, r.position.z, Tol, $"z={z}: прижатие к стене");
        }
    }

    [Test]
    public void BoardOnFloor_SnapsToNearbyBoard()
    {
        // Верх пола на y = 0 → деталь «стоит» при центре y = 0.200.
        var a = Std("A", new Vector3(0.5f, 0.200f, 0.3f));
        var scene = new List<ElementGeometry> { Floor(), a };

        var r = Snap(MakeStd("B"), scene, new Vector3(0.5f, 0.200f, 0.34f)); // зазор 22 мм

        Assert.IsTrue(r.snapped, "контакт с полом не должен маскировать соседа");
        Assert.AreEqual("A", r.targetName);
        Assert.AreEqual(0.318f, r.position.z, Tol, "заподлицо с гранью A (0.309 + 0.009)");
        Assert.AreEqual(0.200f, r.position.y, Tol, "по Y не сдвинулась — контакт с полом цел");
    }

    [Test]
    public void BoardOnFloor_NoNeighbors_StaysPut()
    {
        // Идемпотентность: единственный контакт (пол) подтверждается на месте.
        var pos = new Vector3(0.5f, 0.200f, 0.3f);
        var r = Snap(MakeStd("A"), new List<ElementGeometry> { Floor() }, pos);

        Assert.IsTrue(r.snapped, "контакт с полом подтверждается");
        Assert.AreEqual(pos.x, r.position.x, Tol);
        Assert.AreEqual(pos.y, r.position.y, Tol);
        Assert.AreEqual(pos.z, r.position.z, Tol);
    }

    [Test]
    public void SnapThatWouldBreakExistingContact_IsRejected()
    {
        // Полка нависает в 30 мм над деталью, стоящей на полу: прилипание к ней
        // потребовало бы оторвать деталь от пола — такой снэп отвергается.
        var shelf = At(Make("Shelf", new Vector3Int(800, 18, 400)),
            new Vector3(0.5f, 0.448f, 0.5f)); // низ на y = 0.439
        var scene = new List<ElementGeometry> { Floor(), shelf };

        var r = Snap(MakeStd("A"), scene, new Vector3(0.5f, 0.200f, 0.5f));

        Assert.IsTrue(r.snapped, "контакт с полом подтверждён");
        Assert.AreEqual(0.200f, r.position.y, Tol, "деталь НЕ оторвалась от пола ради полки");
    }

    [Test]
    public void FreeBoard_NearestWins()
    {
        // Без существующих контактов поведение прежнее: ближайшая цель.
        var scene = new List<ElementGeometry>
        {
            Std("A", new Vector3(0f, 0.5f, 0f)),
            Std("B", new Vector3(0f, 0.5f, 0.5f)),
        };

        var r = Snap(MakeStd("M"), scene, new Vector3(0f, 0.5f, 0.04f)); // 13 мм до A

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("A", r.targetName, "ближайшая цель побеждает");
        Assert.AreEqual(0.018f, r.position.z, Tol, "заподлицо с гранью A (0.009 + 0.009)");
    }
}
