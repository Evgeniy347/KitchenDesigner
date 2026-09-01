using System;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

/// <summary>
/// Арифметика углов, которую видит пользователь в полях «X, °», «Y, °», «Z, °».
///
/// Кватернион раскладывается в углы Эйлера не единственным способом, поэтому
/// «показать то, что вернул transform.eulerAngles» — это показать случайное из
/// эквивалентных представлений. Отсюда и жалоба: при Y=90 нажатие «X 90°»
/// меняло поле Z. Здесь считается ровно то, что показывается: шаг всегда
/// ложится на НАЗВАННУЮ ось, значение живёт в [0, 360) и округляется до той же
/// десятой доли градуса, что и вывод.
/// </summary>
public class RotationStepsTests
{
    [Test]
    public void Normalize_AngleBeyondFullTurn_WrapsIntoTheDisplayedRange()
    {
        Assert.AreEqual(10f, RotationSteps.Normalize(370f), 1e-4f,
            "поле показывает 10°, а не 370° — иначе после четырёх нажатий там 360");
        Assert.AreEqual(0f, RotationSteps.Normalize(360f), 1e-4f);
    }

    [Test]
    public void Normalize_NegativeAngle_WrapsUpToPositive()
    {
        Assert.AreEqual(270f, RotationSteps.Normalize(-90f), 1e-4f,
            "Unity показывает углы как 0..360, минус в поле выглядел бы чужеродно");
    }

    [Test]
    public void Normalize_FloatNoise_SnapsToTheDisplayedPrecision()
    {
        Assert.AreEqual(90f, RotationSteps.Normalize(89.99999f), 1e-4f,
            "кватернион возвращает 89.99999 — пользователь должен видеть ровно 90,0");
        Assert.AreEqual(0f, RotationSteps.Normalize(359.96f), 1e-4f,
            "359,96 округляется до 360,0 и обязано превратиться в 0, а не остаться 360");
    }

    [Test]
    public void Normalize_TinyNegativeAngle_NeverShowsAsMinusZero()
    {
        Assert.AreEqual("0.0", RotationSteps.Normalize(-0.04f).ToString("F1",
            System.Globalization.CultureInfo.InvariantCulture),
            "-0,0 в поле угла — артефакт знака нуля у float, а не значение");
    }

    [Test]
    public void Normalize_NotANumber_FallsBackToZero()
    {
        Assert.AreEqual(0f, RotationSteps.Normalize(float.NaN), 1e-4f);
        Assert.AreEqual(0f, RotationSteps.Normalize(float.PositiveInfinity), 1e-4f);
    }

    [Test]
    public void Step_AroundX_MovesOnlyTheXValue()
    {
        var stepped = RotationSteps.Step(new Vector3(0f, 90f, 0f), RotationAxis.X, 90f);

        Assert.AreEqual(90f, stepped.x, 1e-4f, "нажали «X 90°» — растёт X");
        Assert.AreEqual(90f, stepped.y, 1e-4f, "чужие оси не трогаем");
        Assert.AreEqual(0f, stepped.z, 1e-4f,
            "ровно этот Z и уезжал в жалобе: при Y=90 кнопка X меняла поле Z");
    }

    [Test]
    public void Step_AroundY_MovesOnlyTheYValue()
    {
        var stepped = RotationSteps.Step(new Vector3(90f, 0f, 45f), RotationAxis.Y, 90f);

        Assert.AreEqual(90f, stepped.x, 1e-4f);
        Assert.AreEqual(90f, stepped.y, 1e-4f);
        Assert.AreEqual(45f, stepped.z, 1e-4f);
    }

    [Test]
    public void Step_AroundZ_MovesOnlyTheZValue()
    {
        var stepped = RotationSteps.Step(new Vector3(90f, 90f, 0f), RotationAxis.Z, 90f);

        Assert.AreEqual(90f, stepped.x, 1e-4f);
        Assert.AreEqual(90f, stepped.y, 1e-4f);
        Assert.AreEqual(90f, stepped.z, 1e-4f);
    }

    [Test]
    public void Step_HalfTurn_LandsOn180()
    {
        Assert.AreEqual(180f, RotationSteps.Step(Vector3.zero, RotationAxis.Y, 180f).y, 1e-4f,
            "кнопка «Y 180°» у окна ходит тем же путём, что и «Y 90°»");
    }

    [Test]
    public void Step_FourQuarterTurns_ReturnToZero()
    {
        var euler = Vector3.zero;
        for (int i = 0; i < 4; i++) euler = RotationSteps.Step(euler, RotationAxis.X, 90f);

        Assert.AreEqual(0f, euler.x, 1e-4f, "четыре четверти оборота — это ноль, а не 360");
    }

    [Test]
    public void Step_NoisyInput_IsCleanedBeforeTheStep()
    {
        var stepped = RotationSteps.Step(new Vector3(89.99999f, 0f, 0f), RotationAxis.X, 90f);

        Assert.AreEqual(180f, stepped.x, 1e-4f,
            "шаг считается от показанного значения, поэтому 89,99999 + 90 = 180, а не 179,99999");
    }

    [Test]
    public void Step_AxisOutsideTheEnum_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RotationSteps.Step(Vector3.zero, (RotationAxis)3, 90f),
            "перечисление закрывает три оси; чужое значение — ошибка, а не молчаливый пропуск поворота");
    }
}
