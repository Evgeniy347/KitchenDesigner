using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Зафиксированные АРХИТЕКТУРНЫЕ ограничения прилипания (категория
/// "KnownLimitation"). Это не баги, а сознательные границы текущей модели
/// «контакт только между параллельными гранями». Тесты фиксируют фактическое
/// поведение, чтобы изменения были осознанными.
/// </summary>
[Category("KnownLimitation")]
public class SnapKnownLimitationTests : SnapTestBase
{
    // Произвольный поворот (45°/30°) делает грани непараллельными (|dot|<0.999) —
    // снэп их не находит. Доска подносится СБОКУ, где параллельны только
    // повёрнутые грани (грани ±Y слишком далеко по нормали).

    [Test]
    public void Rotated45AroundY_FacesNotParallel_NoSnap()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero, Quaternion.AngleAxis(45f, Vector3.up));
        AssertNotSnapped(b, a, new Vector3(0.83f, 0f, 0f),
            "45° — нет параллельных граней у бокового контакта");
    }

    [Test]
    public void Rotated30AroundY_FacesNotParallel_NoSnap()
    {
        var a = MakeStd("A", Vector3.zero);
        var b = MakeStd("B", Vector3.zero, Quaternion.AngleAxis(30f, Vector3.up));
        AssertNotSnapped(b, a, new Vector3(0.83f, 0f, 0f));
    }

    [Test]
    public void BothRotatedSame45_FacesParallel_Snaps()
    {
        // Контраст: если ОБЕ доски повёрнуты одинаково, их грани снова параллельны
        // друг другу — снэп работает. Показывает, что ограничение именно в
        // относительной непараллельности, а не в повороте как таковом.
        var rot = Quaternion.AngleAxis(45f, Vector3.up);
        var a = MakeStd("A", Vector3.zero, rot);
        var b = MakeStd("B", Vector3.zero, rot);
        // встык вдоль локальной ширины доски, повёрнутой на 45°
        Vector3 dir = rot * Vector3.right;
        var r = Snap(b, a, dir * 0.83f);
        Assert.IsTrue(r.snapped, "одинаково повёрнутые доски имеют параллельные грани");
    }

    [Test]
    public void Threshold_CannotBeZero_ClampedTo1mm()
    {
        // Документируем: порог снэпа нельзя выставить в 0 — кламп к 1 мм.
        KitchenSettings.Instance.SnapThreshold = 0f;
        Assert.AreEqual(1f, KitchenSettings.Instance.SnapThreshold, 0.0001f);
    }
}
