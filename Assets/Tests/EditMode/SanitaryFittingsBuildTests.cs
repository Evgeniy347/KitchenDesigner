using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Фабрика обязана вернуть ПОСТРОЕННЫЙ элемент, а не голый объект с
/// компонентом. Смеситель и стойка нарушали это ровно на умолчательной
/// спецификации, и разобрать этот дефект стоит целиком, потому что ловушка
/// общая для всякого типа с ВЫЧИСЛЯЕМЫМ габаритом.
///
/// Меш строится из ApplyDimensions. Каждая другая фабрика в проекте зовёт его
/// не глядя — присваиванием DimensionsMM, потому что размер у неё приходит
/// аргументом. У этих двух размер выводится из формы, присваивать было нечего,
/// и построение осталось на сеттерах свойств формы. А сеттер, получив то же
/// значение, что уже лежит в поле, выходит НЕ ТРОНУВ ничего.
///
/// Отсюда — на умолчательной спецификации не срабатывал ни один из шести
/// (восьми) сеттеров, ApplyDimensions не звали, и элемент оставался без меша,
/// без рендерера и БЕЗ КОЛЛАЙДЕРА: его нельзя было ни увидеть, ни выделить
/// мышью. В приложении дефект замаскирован — там элемент оживает, и Awake
/// зовёт ApplyDimensions сам; в EditMode Awake не вызывается вовсе, и потому
/// его не видел никто, кроме прогона.
///
/// Почему это не поймали уже написанные тесты — самое полезное здесь. И
/// сверка паритета, и проверки дублирования заводят элемент НЕЗАВОДСКИМИ
/// числами, и совершенно правильно: значение, совпавшее с умолчанием, не
/// отличить от потерянного. Но ровно эти незаводские числа заставляли сеттер
/// сработать, элемент строился по дороге, и все они были зелёными. Два вопроса
/// требуют ПРОТИВОПОЛОЖНЫХ входных данных: «донесли ли значение» проверяется
/// незаводским, «построился ли вообще» — только заводским.
///
/// Копия проверяется отдельно: дублирование зовёт ту же фабрику со
/// спецификацией оригинала, поэтому копия умолчательного элемента шла тем же
/// мёртвым путём. Восстановление сцены зовёт её же.</summary>
public class SanitaryFittingsBuildTests
{
    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private static void AssertBuilt(GameObject go, Vector3Int expectedDims, string what)
    {
        var element = go.GetComponent<KitchenElement>();
        Assert.IsNotNull(element, what + ": фабрика обязана вернуть объект с элементом");

        var filter = go.GetComponent<MeshFilter>();
        Assert.IsNotNull(filter, what + ": нет MeshFilter — ApplyDimensions не звали ни разу, "
            + "и элемент вышел из фабрики пустым объектом");
        Assert.IsNotNull(filter!.sharedMesh, what + ": MeshFilter без меша");
        Assert.Greater(filter.sharedMesh!.vertexCount, 0,
            what + ": меш пустой — протяжка труб ничего не построила");

        Assert.IsNotNull(go.GetComponent<MeshRenderer>(),
            what + ": нет MeshRenderer — элемент невидим, и красить его нечем: "
            + "MaterialManager и пипетка ищут рендерер именно здесь");

        var collider = go.GetComponent<MeshCollider>();
        Assert.IsNotNull(collider, what + ": нет коллайдера — элемент нельзя выделить мышью, "
            + "и это хуже, чем невидимость: невидимое хотя бы заметно сразу");
        Assert.IsNotNull(collider!.sharedMesh, what + ": коллайдер без меша");

        Assert.AreEqual(expectedDims, element!.DimensionsMM,
            what + ": габарит вычисляется в перестройке, и умолчательный габарит вместо "
            + "вычисленного значит, что перестройки не было");
    }

    [Test]
    public void ANewBathMixer_AtItsDefaultSpec_IsFullyBuilt()
    {
        var spec = BathMixerSpec.Default;

        AssertBuilt(ElementFactory.CreateBathMixer(spec, "Смеситель", Vector3.zero),
            BathMixerLayout.DimensionsMM(spec), "смеситель на умолчаниях");
    }

    [Test]
    public void ANewShowerColumn_AtItsDefaultSpec_IsFullyBuilt()
    {
        var spec = ShowerColumnSpec.Default;

        AssertBuilt(ElementFactory.CreateShowerColumn(spec, "Стойка", Vector3.zero),
            ShowerColumnLayout.DimensionsMM(spec), "стойка на умолчаниях");
    }

    [Test]
    public void ADuplicatedBathMixer_AtItsDefaultSpec_IsFullyBuilt()
    {
        var spec = BathMixerSpec.Default;
        var source = ElementFactory.CreateBathMixer(spec, "Смеситель", Vector3.zero)
            .GetComponent<KitchenElement>();
        Assert.IsNotNull(source, "фабрика обязана вернуть объект с элементом");

        AssertBuilt(ElementFactory.Duplicate(source!), BathMixerLayout.DimensionsMM(spec),
            "копия смесителя на умолчаниях");
    }

    [Test]
    public void ADuplicatedShowerColumn_AtItsDefaultSpec_IsFullyBuilt()
    {
        var spec = ShowerColumnSpec.Default;
        var source = ElementFactory.CreateShowerColumn(spec, "Стойка", Vector3.zero)
            .GetComponent<KitchenElement>();
        Assert.IsNotNull(source, "фабрика обязана вернуть объект с элементом");

        AssertBuilt(ElementFactory.Duplicate(source!), ShowerColumnLayout.DimensionsMM(spec),
            "копия стойки на умолчаниях");
    }
}
