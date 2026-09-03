using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Оболочка коллайдера у элементов, которые строят СВОЙ меш через общий
/// помощник (<c>OwnedMeshBody</c> и профильная надстройка над ним).
///
/// Зачем это закреплено отдельно. Помощник выбирает между выпуклой оболочкой и
/// точным невыпуклым мешем, и выбор этот НЕ косметический: выпуклая оболочка
/// вокруг душевой стойки — петля шланга и лейка на отлёте — превращается в
/// кликабельную кляксу поверх всего вокруг, и соседний элемент перестаёт
/// выделяться мышью. Обратная ошибка тише, но дороже: невыпуклый коллайдер у
/// мебели не участвует в физике как объём.
///
/// До этого набора слово <c>convex</c> не встречалось в тестах НИ РАЗУ. То есть
/// флаг, заведённый ради стойки, не был закреплён ничем, и любая перестановка
/// внутри помощника снимала его молча — ни один прогон не позеленел бы иначе.
/// Набор написан именно для такой перестановки: сначала зелёный на старом коде,
/// потом он же — доказательство, что слияние ничего не сдвинуло.
///
/// Проверка идёт ЗАПУСКОМ фабрики, а не грепом по исходникам, и намеренно не
/// называет ни одного класса-помощника: она обязана пережить его переименование
/// и остаться про наблюдаемое поведение элемента.</summary>
public class MeshBodyColliderContractTests
{
    /// <summary>Девять типов, чей корневой меш строит общий помощник. Ожидание
    /// выписано поимённо, а не выведено из кода: правило, посчитанное тем же
    /// выражением, что и проверяемое значение, сходится всегда.</summary>
    private static readonly (Type type, bool convex, string why)[] Expected =
    {
        (typeof(StoolElement), true, "сиденье — выдавленный скруглённый прямоугольник, он выпуклый и так"),
        (typeof(PouffeElement), true, "тумба пуфика — то же выдавливание"),
        (typeof(ChairElement), true, "сиденье стула — то же выдавливание"),
        (typeof(SofaElement), true, "основание дивана — то же выдавливание"),
        (typeof(BedElement), true, "рама кровати — то же выдавливание"),
        (typeof(RadiusTableElement), true, "радиусная столешница — то же выдавливание"),
        (typeof(BathtubElement), true, "чаша вогнута, но оболочка по габариту ванны кликается верно"),
        (typeof(BathMixerElement), true, "смеситель — компактная протяжка труб"),
        (typeof(ShowerColumnElement), false,
            "ЕДИНСТВЕННЫЙ невыпуклый: у стойки петля шланга и лейка на отлёте, и выпуклая "
            + "оболочка накрыла бы кляксой всё пространство между ними — соседний элемент "
            + "перестал бы выделяться мышью"),
    };

    private ProjectLoadStateGuard? _globals;

    [SetUp]
    public void SetUp()
    {
        _globals = ProjectLoadStateGuard.Capture();
        EveryElementType.ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        EveryElementType.ClearScene();
        MaterialManager.ClearCache();
        _globals?.Restore();
    }

    [Test]
    public void EveryElementWithAnOwnedMesh_KeepsTheColliderShellItWasGiven()
    {
        foreach (var (type, convex, why) in Expected)
        {
            var element = EveryElementType.Spawn(type, "оболочка-" + type.Name);

            var collider = element.gameObject.GetComponent<MeshCollider>();
            Assert.IsNotNull(collider,
                type.Name + ": коллайдер живёт на корне — без него элемент не выделить мышью");
            Assert.IsNotNull(collider!.sharedMesh, type.Name + ": коллайдер без меша не ловит луч");

            Assert.AreEqual(convex, collider.convex,
                type.Name + ": оболочка коллайдера сменилась. Ожидалось convex=" + convex
                + ", потому что " + why);

            EveryElementType.ClearScene();
        }
    }

    [Test]
    public void TheColliderAndTheRenderer_ShareOneMesh()
    {
        foreach (var (type, _, _) in Expected)
        {
            var element = EveryElementType.Spawn(type, "меш-" + type.Name);
            var go = element.gameObject;

            var filter = go.GetComponent<MeshFilter>();
            Assert.IsNotNull(filter, type.Name + ": нет MeshFilter — помощник не строил меш вовсе");
            Assert.IsNotNull(filter!.sharedMesh, type.Name + ": MeshFilter без меша");
            Assert.Greater(filter.sharedMesh!.vertexCount, 0, type.Name + ": меш пустой");

            Assert.AreSame(filter.sharedMesh, go.GetComponent<MeshCollider>()!.sharedMesh,
                type.Name + ": видимый меш и меш коллайдера разошлись — кликается не то, "
                + "что нарисовано");

            EveryElementType.ClearScene();
        }
    }

    /// <summary>Сторож самого ожидания. Таблица, в которой все девять строк стали
    /// одинаковыми, зеленеет на любом коде и не проверяет ничего: ровно так
    /// исчезнет флаг стойки, если его «упростить» до общего умолчания.</summary>
    [Test]
    public void TheExpectation_HoldsBothAnswers_OtherwiseItProvesNothing()
    {
        CollectionAssert.Contains(Expected.Select(e => e.convex).ToArray(), true,
            "в таблице не осталось выпуклой оболочки — проверять нечего");
        CollectionAssert.Contains(Expected.Select(e => e.convex).ToArray(), false,
            "в таблице не осталось НЕВЫПУКЛОЙ оболочки: флаг душевой стойки снова "
            + "не закреплён ничем, а набор при этом зелёный");

        foreach (var (type, _, why) in Expected)
            Assert.IsNotEmpty(why, "у строки " + type.Name + " не названа причина");
    }
}
