using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Зафиксированные АРХИТЕКТУРНЫЕ ограничения прилипания (категория
/// "KnownLimitation"). Это не баги, а сознательные границы текущей модели
/// «контакт только между параллельными гранями». Тесты фиксируют фактическое
/// поведение, чтобы изменения были осознанными.
///
/// Кламп порога снэпа к 1 мм проверяется отдельно в сценовой части: он живёт
/// в KitchenSettings, а ядро порог принимает параметром и ничего не клампит.
/// </summary>
[Category("KnownLimitation")]
public class SnapKnownLimitationTests : SnapCoreTestBase
{
    // Произвольный поворот (45°/30°) делает грани непараллельными (|dot|<0.999) —
    // снэп их не находит. Деталь подносится СБОКУ, где параллельны только
    // повёрнутые грани (грани ±Y слишком далеко по нормали).

    [Test]
    public void Rotated45AroundY_FacesNotParallel_NoSnap()
    {
        var a = Std("A", Vector3.zero);
        var b = MakeStd("B", RotY(45f));
        AssertNotSnapped(b, a, new Vector3(0.83f, 0f, 0f),
            "45° — нет параллельных граней у бокового контакта");
    }

    [Test]
    public void Rotated30AroundY_FacesNotParallel_NoSnap()
    {
        var a = Std("A", Vector3.zero);
        var b = MakeStd("B", RotY(30f));
        AssertNotSnapped(b, a, new Vector3(0.83f, 0f, 0f));
    }

    [Test]
    public void BothRotatedSame45_FacesParallel_Snaps()
    {
        // Контраст: если ОБЕ детали повёрнуты одинаково, их грани снова параллельны
        // друг другу — снэп работает. Показывает, что ограничение именно в
        // относительной непараллельности, а не в повороте как таковом.
        var rot = RotY(45f);
        var a = At(MakeStd("A", rot), Vector3.zero);
        var b = MakeStd("B", rot);

        // встык вдоль локальной ширины детали, повёрнутой на 45°
        Vector3 dir = rot * Vector3.right;
        var r = Snap(b, a, dir * 0.83f);

        Assert.IsTrue(r.snapped, "одинаково повёрнутые детали имеют параллельные грани");
    }
}
