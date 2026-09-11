using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Ctrl+D смещал копию всегда по +X, и на виде вдоль X копия пряталась
/// ровно за оригиналом: пользователь нажимал дважды и был уверен, что ничего не
/// произошло. Сторона выбирается по направлению взгляда — та ось, вдоль которой
/// камера смотрит сильнее всего, и знак «на зрителя».
///
/// Решение вынесено сюда, на быстрый путь, именно затем, чтобы весь круг ракурсов
/// проверялся перебором, а не тремя удобными углами: дефект жил в том, КАКОЙ угол
/// попадёт в тест.</summary>
public class DuplicateOffsetTests
{
    private const float Distance = 0.1f;

    private static readonly int[] YawSectorBoundaries = { 45, 135, 225, 315 };

    private static Vector3 InXZ(float degrees) =>
        new Vector3(Mathf.Sin(degrees * Mathf.Deg2Rad), 0f, Mathf.Cos(degrees * Mathf.Deg2Rad));

    private static Vector3 InYZ(float degrees) =>
        new Vector3(0f, Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));

    private static Vector3[] Sweep(System.Func<float, Vector3> direction)
    {
        var answers = new Vector3[360];
        for (int degrees = 0; degrees < 360; degrees++)
            answers[degrees] = DuplicateOffset.ForViewDirection(direction(degrees), Distance);
        return answers;
    }

    private static bool IsOneOfTheSixSides(Vector3 offset)
    {
        int nonZero = 0;
        if (offset.x != 0f) nonZero++;
        if (offset.y != 0f) nonZero++;
        if (offset.z != 0f) nonZero++;
        return nonZero == 1 && Mathf.Abs(offset.magnitude - Distance) < 1e-6f;
    }

    private static List<int> SwitchAngles(Vector3[] answers)
    {
        var switches = new List<int>();
        for (int degrees = 0; degrees < answers.Length; degrees++)
            if (answers[degrees] != answers[(degrees + answers.Length - 1) % answers.Length])
                switches.Add(degrees);
        return switches;
    }

    /// <summary>Ответ обязан быть СТОРОНОЙ, а не наклонным вектором: копия смещается
    /// по одной оси на полную величину, иначе она уезжает по диагонали и перестаёт
    /// попадать в сетку.</summary>
    [Test]
    public void ForViewDirection_YawSweep_AlwaysAnswersOneOfTheSixSides()
    {
        var offenders = new List<string>();
        var answers = Sweep(InXZ);

        for (int degrees = 0; degrees < answers.Length; degrees++)
            if (!IsOneOfTheSixSides(answers[degrees])) offenders.Add($"{degrees}° -> {answers[degrees]}");

        CollectionAssert.IsEmpty(offenders,
            "на каждом ракурсе ответ — одна из шести сторон длиной ровно в шаг дублирования");
    }

    /// <summary>Смысл всей задачи одной строкой: копия выходит НА ЗРИТЕЛЯ, поэтому
    /// проекция смещения на взгляд отрицательна под любым углом.</summary>
    [Test]
    public void ForViewDirection_YawSweep_AlwaysMovesTheCopyTowardsTheViewer()
    {
        var offenders = new List<string>();

        for (int degrees = 0; degrees < 360; degrees++)
        {
            var view = InXZ(degrees);
            float towardsScene = Vector3.Dot(DuplicateOffset.ForViewDirection(view, Distance), view);
            if (towardsScene >= 0f) offenders.Add($"{degrees}° -> {towardsScene}");
        }

        CollectionAssert.IsEmpty(offenders,
            "копия обязана появляться перед оригиналом, то есть ближе к камере");
    }

    /// <summary>Граница секторов обязана быть ПЕРЕКЛЮЧАТЕЛЕМ: четыре смены на полный
    /// круг, каждая на биссектрисе. Дрожание — это когда ответ меняется туда-сюда
    /// внутри сектора, и ловится оно только счётом смен, а не выборочными углами.</summary>
    [Test]
    public void ForViewDirection_YawSweep_SwitchesFourTimes_OnlyAtSectorBoundaries()
    {
        var answers = Sweep(InXZ);

        var switches = SwitchAngles(answers);

        Assert.AreEqual(4, switches.Count,
            $"на круг ровно четыре смены стороны, а найдено: {string.Join(", ", switches)}");
        foreach (int degrees in switches)
        {
            int nearest = int.MaxValue;
            foreach (int boundary in YawSectorBoundaries)
                nearest = Mathf.Min(nearest, Mathf.Abs(degrees - boundary));
            Assert.LessOrEqual(nearest, 1,
                $"смена на {degrees}° стоит не на биссектрисе сектора");
        }
    }

    /// <summary>Четыре сектора — четыре РАЗНЫЕ стороны, и ни одной вертикальной:
    /// ответ, повторившийся в двух секторах, означал бы, что одна из сторон
    /// недостижима вовсе.</summary>
    [Test]
    public void ForViewDirection_YawSweep_UsesAllFourHorizontalSides_AndNoVerticalOne()
    {
        var answers = Sweep(InXZ);

        var distinct = new HashSet<Vector3>(answers);

        CollectionAssert.AreEquivalent(
            new[]
            {
                new Vector3(Distance, 0f, 0f), new Vector3(-Distance, 0f, 0f),
                new Vector3(0f, 0f, Distance), new Vector3(0f, 0f, -Distance),
            },
            distinct,
            "горизонтальный облёт обязан дать все четыре горизонтальные стороны и ни одной вертикальной");
    }

    /// <summary>Вид сверху и вид снизу — такие же ракурсы, как остальные: с них копия
    /// уходит по Y. Без этого теста вертикальные стороны остались бы мёртвым кодом.</summary>
    [Test]
    public void ForViewDirection_PitchSweep_ReachesBothVerticalSides()
    {
        var answers = Sweep(InYZ);

        var distinct = new HashSet<Vector3>(answers);

        CollectionAssert.AreEquivalent(
            new[]
            {
                new Vector3(0f, Distance, 0f), new Vector3(0f, -Distance, 0f),
                new Vector3(0f, 0f, Distance), new Vector3(0f, 0f, -Distance),
            },
            distinct,
            "облёт в вертикальной плоскости обязан дать обе вертикальные стороны");
        Assert.AreEqual(4, SwitchAngles(answers).Count,
            "и здесь граница переключает ответ, а не заставляет его дрожать");
    }

    [Test]
    public void ForViewDirection_LookingStraightDown_PutsTheCopyAbove()
    {
        Assert.AreEqual(new Vector3(0f, Distance, 0f),
            DuplicateOffset.ForViewDirection(Vector3.down, Distance),
            "камера смотрит вниз — зритель сверху, копия обязана лечь поверх оригинала");
    }

    /// <summary>Когда камеры нет (пакетный прогон, MCP без сцены), направления тоже нет,
    /// и смещение остаётся тем самым историческим «+X»: ноль вместо стороны сделал бы
    /// копию невидимой внутри оригинала.</summary>
    [Test]
    public void ForViewDirection_NoDirectionAtAll_KeepsTheHistoricPlusXSide()
    {
        Assert.AreEqual(new Vector3(Distance, 0f, 0f),
            DuplicateOffset.ForViewDirection(Vector3.zero, Distance),
            "без взгляда смещение всё равно обязано быть ненулевым");
        Assert.AreEqual(new Vector3(Distance, 0f, 0f),
            DuplicateOffset.ForViewDirection(new Vector3(float.NaN, float.NaN, float.NaN), Distance),
            "NaN не выбирает ось: он обязан попасть в ту же запасную сторону, а не в ветку сравнений");
    }
}
