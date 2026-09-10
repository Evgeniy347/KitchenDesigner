using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>PipeFittingElement.GetSpecItems называло строку по BoreSizeId — «самый широкий»
/// из отводов (PipeSizes.Widest). Тройник 25×25×20 уходил в ведомость как «Тройник ДН25»,
/// теряя переход, а фитинг без единого известного диаметра получал в имя ДН20 из воздуха
/// (Widest подставляет умолчание, когда все отводы null). `TakeBoreSizes` — тот же метод,
/// которым `PipeFittingSizeLink.ApplyAll` кормит фитинг выведенными на сцене диаметрами,
/// так что эти тесты бьют по реальному пути данных, не только по чистой формуле имени.
/// </summary>
public class PipeFittingSpecItemsTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private PipeFittingElement Fitting(GameObject go)
    {
        _spawned.Add(go);
        var fitting = go.GetComponent<PipeFittingElement>();
        fitting.ApplyDimensions();
        return fitting;
    }

    private static string NameOf(PipeFittingElement fitting) =>
        fitting.GetSpecItems(new KitchenElement[0]).Single().name;

    [Test]
    public void ReducingTee_NamesAllThreeBores_NotOnlyTheWidest()
    {
        var tee = Fitting(ElementFactory.CreatePipeTee("Tee", Vector3.zero));
        tee.TakeBoreSizes(new string?[] { PipeSpec.Dn25, PipeSpec.Dn25, PipeSpec.Dn20 });
        tee.ApplyDimensions();

        Assert.AreEqual("Тройник ДН25×25×20", NameOf(tee),
            "переход тройника обязан остаться видимым в имени строки, иначе по ведомости "
            + "закажут ДН25 везде и деталь для отвода ДН20 окажется не той");
    }

    /// <summary>Противоположный вход: тройник без перехода (все три отвода — ДН20)
    /// называет их одинаково, а не сворачивает три известных числа в одно.</summary>
    [Test]
    public void EqualTee_NamesAllThreeBoresTheSame()
    {
        var tee = Fitting(ElementFactory.CreatePipeTee("Tee", Vector3.zero));
        tee.TakeBoreSizes(new string?[] { PipeSpec.Dn20, PipeSpec.Dn20, PipeSpec.Dn20 });
        tee.ApplyDimensions();

        Assert.AreEqual("Тройник ДН20×20×20", NameOf(tee));
    }

    [Test]
    public void FittingWithNothingConnected_ShowsADashInTheName_NotAMadeUpDefault()
    {
        var cap = Fitting(ElementFactory.CreatePipeCap("Plug", Vector3.zero));

        Assert.AreEqual("Заглушка ДН—", NameOf(cap),
            "фитинг без единого известного отвода обязан показать прочерк — прежний код "
            + "подставлял ДН20 через PipeSizes.Widest, и пользователь покупал деталь по "
            + "числу, взятому из воздуха");
    }

    /// <summary>Противоположный вход: та же переходная муфта, но с одной известной
    /// стороной — известная сторона называет число, неизвестная остаётся прочерком, а
    /// не унаследовала число известной стороны (стороны переходной муфты независимы —
    /// PipeFittingBoreTests.ATransitionCoupling_*).</summary>
    [Test]
    public void CouplingWithOneSideKnown_ShowsTheNumberOnThatSide_AndADashOnTheOther()
    {
        var sleeve = Fitting(ElementFactory.CreatePipeCoupling("Sleeve", Vector3.zero));
        sleeve.TakeBoreSizes(new string?[] { PipeSpec.Dn20, null });
        sleeve.ApplyDimensions();

        Assert.AreEqual("Переходная муфта ДН20×—", NameOf(sleeve),
            "известная сторона называет свой диаметр; свободная сторона осталась " +
            "прочерком, а не унаследовала чужое число и не потерялась в умолчании");
    }
}
