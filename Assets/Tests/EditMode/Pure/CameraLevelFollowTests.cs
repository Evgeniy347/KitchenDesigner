using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// L5b (план LEVELS): камера обязана сдвигаться по Y на разницу отметок при смене этажа
/// и точно возвращаться при обратном переключении — «1->2->1 возвращает камеру точно».
/// </summary>
public class CameraLevelFollowTests
{
    [Test]
    public void ShiftTargetForLevelChange_MovesOnlyY_ByTheElevationDelta()
    {
        var target = new Vector3(1.5f, 1.2f, -0.7f);

        var shifted = CameraLevelFollow.ShiftTargetForLevelChange(target, 0, 3000);

        Assert.AreEqual(1.5f, shifted.x, Tolerance.EpsilonUnits, "X не участвует в смене этажа");
        Assert.AreEqual(-0.7f, shifted.z, Tolerance.EpsilonUnits, "Z не участвует в смене этажа");
        Assert.AreEqual(1.2f + 3f, shifted.y, Tolerance.EpsilonUnits,
            "Y обязан сдвинуться ровно на разницу отметок в метрах (3000 мм = 3 м)");
    }

    [Test]
    public void GoingUpThenBackDown_ReturnsExactlyTheOriginalTarget()
    {
        var original = new Vector3(0.3f, 0.9f, 2.1f);

        var atLevel2 = CameraLevelFollow.ShiftTargetForLevelChange(original, 0, 3000);
        var backAtLevel1 = CameraLevelFollow.ShiftTargetForLevelChange(atLevel2, 3000, 0);

        Assert.AreEqual(original.x, backAtLevel1.x, Tolerance.EpsilonUnits);
        Assert.AreEqual(original.y, backAtLevel1.y, Tolerance.EpsilonUnits,
            "1->2->1 обязан вернуть Y с точностью до общего допуска сцены (Tolerance."
            + "EpsilonUnits) — как и любое другое «точное» сравнение позиций в этом "
            + "проекте, а не бит в бит: сложение и вычитание одной и той же дельты "
            + "у произвольного float не гарантирует побитового совпадения");
        Assert.AreEqual(original.z, backAtLevel1.z, Tolerance.EpsilonUnits);
    }

    [Test]
    public void SameLevel_IsANoOp()
    {
        var target = new Vector3(4f, -1f, 0.25f);

        var result = CameraLevelFollow.ShiftTargetForLevelChange(target, 3000, 3000);

        Assert.AreEqual(target, result, "переключение на тот же уровень не имеет права сдвинуть камеру");
    }

    [Test]
    public void GoingDown_SubtractsTheDelta()
    {
        var target = new Vector3(0f, 5f, 0f);

        var shifted = CameraLevelFollow.ShiftTargetForLevelChange(target, 3000, 0);

        Assert.AreEqual(5f - 3f, shifted.y, Tolerance.EpsilonUnits);
    }
}
