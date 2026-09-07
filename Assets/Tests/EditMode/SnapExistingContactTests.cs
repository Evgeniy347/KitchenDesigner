using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Баг «в конкретных позициях не прилипает»: кандидат с нулевым сдвигом
/// (деталь УЖЕ заподлицо с соседом/полом) побеждал содержательные снэпы,
/// и деталь, скользящая по грани соседа, никогда не прилипала к стене.
/// Правило после фикса: содержательный снэп предпочтительнее подтверждения
/// текущего контакта, но не должен этот контакт разрывать (сдвиг ⊥ нормалям
/// существующих контактов). Сцена в тестах — снятая с реального проекта
/// пользователя через мост (2026-07-04).
/// </summary>
public class SnapExistingContactTests : SnapTestBase
{
    // --- Реальная сцена: лежащая деталь прижата торцом к стоящей, рядом стена ---

    private KitchenElement? _standing;
    private KitchenElement? _lying;
    private KitchenElement? _wall;

    private void BuildUserScene()
    {
        MakeFloor();
        _standing = Make("Standing", new Vector3Int(600, 720, 19),
            new Vector3(-0.8905f, 0.449476f, 0.7418f), ManagedRotation.Euler(0f, 90f, 180f));
        _lying = Make("Lying", new Vector3Int(600, 720, 19),
            new Vector3(-1.2f, 0.098976f, 1.026f), ManagedRotation.Euler(270f, 0f, 0f));
        _wall = Make("Wall", new Vector3Int(2800, 2500, 100),
            new Vector3(-0.1f, 1.25f, 1.45f));
    }

    private List<KitchenElement> Others() => new List<KitchenElement>(_spawned.ConvertAll(
        go => go.GetComponent<KitchenElement>()));

    [Test]
    public void LyingBoard_SlidingAlongNeighbor_SnapsToWall()
    {
        BuildUserScene();

        // Торец лежащей на x=-0.9 заподлицо с гранью стоящей (x=-0.9) — контакт
        // сохраняется при движении по Z. Задняя кромка на z+0.36; стена на 1.40.
        var testPos = new Vector3(-1.2f, 0.098976f, 1.03f); // зазор до стены 10мм
        var r = SnapSystem.TrySnap(_lying!, Others(), testPos);

        Assert.IsTrue(r.snapped, "должна прилипнуть к стене");
        Assert.AreEqual("Wall", r.targetName,
            "цель — стена (контакт со стоящей деталью не должен её маскировать)");
        Assert.AreEqual(1.04f, r.position.z, Tol, "прижатие задней кромки к стене (1.40-0.36)");
        Assert.AreEqual(testPos.x, r.position.x, Tol, "сдвиг только по Z");
        Assert.AreEqual(testPos.y, r.position.y, Tol, "сдвиг только по Z");
    }

    [Test]
    public void LyingBoard_WholeUserRange_SnapsToWall()
    {
        BuildUserScene();

        // Диапазон из бага: z от 1.026 до ~1.039 (ближе к 1.04 начинается
        // пересечение со стеной — отдельное известное ограничение снэпа).
        float[] zs = { 1.026f, 1.030f, 1.035f, 1.039f };
        foreach (var z in zs)
        {
            var r = SnapSystem.TrySnap(_lying!, Others(), new Vector3(-1.2f, 0.098976f, z));
            Assert.IsTrue(r.snapped, $"z={z}: должна прилипнуть");
            Assert.AreEqual("Wall", r.targetName, $"z={z}: цель — стена");
            Assert.AreEqual(1.04f, r.position.z, Tol, $"z={z}: прижатие к стене");
        }
    }

    // --- Общий случай: деталь стоит на полу и должна липнуть к соседу ---

    [Test]
    public void BoardOnFloor_SnapsToNearbyBoard()
    {
        // Вдали от центра/кромок пола, чтобы контакт с полом был чистым
        // «нулевым» кандидатом (без притяжения к кромке/центру пола).
        // Верх пола на y=0.009 → деталь «стоит» при центре y=0.209.
        MakeFloor();
        var a = MakeStd("A", new Vector3(0.5f, 0.209f, 0.3f));
        var b = MakeStd("B", new Vector3(0.5f, 0.209f, 0.34f)); // зазор граней 22мм

        var r = SnapSystem.TrySnap(b, Others(), b.transform.position);

        Assert.IsTrue(r.snapped, "контакт с полом не должен маскировать соседа");
        Assert.AreEqual("A", r.targetName);
        Assert.AreEqual(0.318f, r.position.z, Tol, "заподлицо с гранью A (0.309+0.009)");
        Assert.AreEqual(0.209f, r.position.y, Tol, "по Y не сдвинулась — контакт с полом цел");
    }

    [Test]
    public void BoardOnFloor_NoNeighbors_StaysPut()
    {
        // Идемпотентность: единственный контакт (пол) подтверждается на месте.
        MakeFloor();
        var a = MakeStd("A", new Vector3(0.5f, 0.209f, 0.3f));

        var r = SnapSystem.TrySnap(a, Others(), a.transform.position);

        Assert.IsTrue(r.snapped, "контакт с полом подтверждается");
        Assert.AreEqual(a.transform.position.x, r.position.x, Tol);
        Assert.AreEqual(a.transform.position.y, r.position.y, Tol);
        Assert.AreEqual(a.transform.position.z, r.position.z, Tol);
    }

    [Test]
    public void SnapThatWouldBreakExistingContact_IsRejected()
    {
        // Полка нависает в 30мм над стоящей на полу деталью: прилипание к ней
        // потребовало бы оторвать деталь от пола (сдвиг вдоль нормали пола) —
        // такой снэп отвергается, деталь остаётся на месте.
        MakeFloor();
        var a = MakeStd("A", new Vector3(0.5f, 0.209f, 0.5f)); // на полу, верх на y=0.409
        Make("Shelf", new Vector3Int(800, 18, 400),
            new Vector3(0.5f, 0.448f, 0.5f)); // низ на y=0.439, зазор 30мм

        var r = SnapSystem.TrySnap(a, Others(), a.transform.position);

        Assert.IsTrue(r.snapped, "контакт с полом подтверждён");
        Assert.AreEqual(0.209f, r.position.y, Tol, "деталь НЕ оторвалась от пола ради полки");
    }

    [Test]
    public void FreeBoard_BehavesAsBefore_NearestWins()
    {
        // Без существующих контактов поведение прежнее: ближайший снэп.
        var a = MakeStd("A", new Vector3(0f, 0.5f, 0f));
        var b = MakeStd("B", new Vector3(0f, 0.5f, 0.5f));
        var moved = MakeStd("M", Vector3.zero);

        var pos = new Vector3(0f, 0.5f, 0.04f); // 13мм до A, далеко от B
        var r = SnapSystem.TrySnap(moved, Others(), pos);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("A", r.targetName, "ближайшая цель побеждает, как раньше");
        Assert.AreEqual(0.018f, r.position.z, Tol, "заподлицо с гранью A (0.009+0.009)");
    }
}
