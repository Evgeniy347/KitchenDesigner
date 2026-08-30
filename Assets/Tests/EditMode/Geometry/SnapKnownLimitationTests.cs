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

    /// <summary>Второе ограничение той же природы, найденное при разборе
    /// комментариев ядра: кандидат отбрасывается, если ЦЕНТР детали попадает
    /// внутрь ГАБАРИТА соседа. У повёрнутой на 45° детали габарит заведомо шире
    /// тела, поэтому две тонкие панели, стоящие пластью к пласти в 20 мм друг от
    /// друга, снэп теряют — хотя ни одна не внутри другой.
    ///
    /// Пара тестов ниже: ограничение и положительный контроль на ТОЙ ЖЕ
    /// геометрии без поворота. Без контроля первый был бы зелёным и на коде,
    /// который просто не умеет такой стык.</summary>
    [Test]
    public void RotatedPanels_FaceToFace_DoNotSnap_TheirCentreFallsInTheFatAabb()
    {
        var rot = RotY(45f);
        var a = At(MakeStd("A", rot), rot * new Vector3(0f, 0f, 0.038f));
        var b = MakeStd("B", rot);

        var r = Snap(b, a, Vector3.zero);

        Assert.IsFalse(r.snapped,
            "зафиксировано ФАКТИЧЕСКОЕ поведение: проверка «центр внутри габарита соседа» "
            + "у повёрнутых деталей срабатывает ложно и съедает стык пласть-к-пласти");
    }

    [Test]
    public void UnrotatedPanels_FaceToFace_DoSnap()
    {
        var a = At(MakeStd("A"), new Vector3(0f, 0f, 0.038f));
        var b = MakeStd("B");

        var r = Snap(b, a, Vector3.zero);

        Assert.IsTrue(r.snapped,
            "положительный контроль: та же геометрия без поворота прилипает, значит дело "
            + "именно в раздутом габарите, а не в пороге или перекрытии");
        Assert.AreEqual(0.020f, r.position.z, Tol, "18 мм толщины — стык пластей");
    }
}
