using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>
/// PlayMode: что остаётся в памяти после уничтожения элемента.
///
/// <c>OnDestroy</c> — СООБЩЕНИЕ Unity, а не виртуальный метод: его зовут по
/// имени. Объявил его наследник — приватный базовый БОЛЬШЕ НЕ ЗОВЁТСЯ, молча,
/// без предупреждения компилятора и без <c>override</c>, который заметили бы в
/// ревью. Так уже дважды пропадало снятие с учёта в <c>PartRegistry</c>, и
/// ровно так же пропадала бы уборка процедурного меша: сцена «работает», а
/// Mesh — это <c>UnityEngine.Object</c>, сборщик мусора его не трогает, и он
/// копится в памяти до конца сессии.
///
/// Поэтому <c>OnDestroy</c> в слое элементов ровно один — базовый; наследники
/// доубирают за собой в <c>KitchenElement.OnElementDestroyed</c>. Проверка
/// живёт в PlayMode, потому что вне Play mode Unity не зовёт ни <c>Awake</c>,
/// ни <c>OnDestroy</c>: под EditMode такой тест зелен всегда и не проверяет
/// ничего. Исходники сторожит <c>UnityMessageShadowingTests</c>.
/// </summary>
public class ElementMeshLifetimeTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
        yield return null;
    }

    private GameObject Spawn(GameObject go)
    {
        _spawned.Add(go);
        return go;
    }

    private static Mesh MeshOf(GameObject go, string who)
    {
        var filter = go.GetComponent<MeshFilter>();
        Assert.IsTrue(filter != null, "предусловие: " + who + " строит себе меш через MeshFilter");
        var mesh = filter!.sharedMesh;
        Assert.IsTrue(mesh != null, "предусловие: меш " + who + " построен, иначе течь нечему");
        return mesh!;
    }

    private void AssertMeshDiesWithTheElement(GameObject go, string who)
    {
        var mesh = MeshOf(go, who);

        Object.DestroyImmediate(go);
        _spawned.Remove(go);

        Assert.IsTrue(mesh == null,
            "процедурный меш " + who + " пережил свой элемент — это утечка: Mesh живёт как "
            + "UnityEngine.Object, сборщик мусора его не тронет. Убирает его базовый "
            + "KitchenElement.OnDestroy; объявив собственный OnDestroy, наследник закрыл бы "
            + "базовый и уборка пропала бы молча");
    }

    [Test]
    public void Pillar_Destroyed_ItsProceduralMeshIsDestroyedToo()
    {
        var pillar = Spawn(ElementFactory.CreatePillar(
            PillarElement.MidHeightMM_Default, "Опора", Vector3.zero));

        AssertMeshDiesWithTheElement(pillar, "опоры");
    }

    [Test]
    public void RadiusTable_Destroyed_ItsProceduralMeshIsDestroyedToo()
    {
        var table = Spawn(ElementFactory.CreateRadiusTable(
            new Vector3Int(1200, 750, 700), "Радиусный стол", Vector3.zero));

        AssertMeshDiesWithTheElement(table, "радиусного стола");
    }

    [Test]
    public void RadialShelf_Destroyed_ItsProceduralMeshIsDestroyedToo()
    {
        var shelf = Spawn(ElementFactory.CreateRadialShelf(
            600, 400, 18, 100, "Полка", Vector3.zero));

        AssertMeshDiesWithTheElement(shelf, "радиусной полки");
    }

    [Test]
    public void Drawer_Destroyed_ItsProceduralMeshIsDestroyedToo()
    {
        var drawer = Spawn(ElementFactory.CreateDrawer(
            DrawerType.A, 350, DrawerColor.Anthracite, 400, "Ящик", Vector3.zero));

        AssertMeshDiesWithTheElement(drawer, "ящика");
    }

    [Test]
    public void AssembledFacade_Destroyed_ItsProceduralMeshIsDestroyedToo()
    {
        var facade = Spawn(ElementFactory.CreateAssembledFacade(
            new Vector3Int(400, 700, 18), "Сборный фасад", Vector3.zero));

        AssertMeshDiesWithTheElement(facade, "сборного фасада");
    }

    /// <summary>Уничтожение и пересборка — одно и то же владение. Полка строит
    /// новый меш на каждое изменение радиуса; не передай она владение базе,
    /// каждый такой пересчёт оставлял бы за собой ещё один Mesh — утечка без
    /// единого уничтоженного объекта.</summary>
    [Test]
    public void RadialShelf_RebuiltWithANewRadius_DoesNotLeaveTheOldMeshBehind()
    {
        var shelf = Spawn(ElementFactory.CreateRadialShelf(
            600, 400, 18, 100, "Полка", Vector3.zero));
        var element = shelf.GetComponent<RadialShelfElement>();
        Assert.IsTrue(element != null, "предусловие: фабрика сделала именно радиусную полку");

        var before = MeshOf(shelf, "радиусной полки");
        element!.CornerRadius = 150;
        var after = MeshOf(shelf, "радиусной полки");

        Assert.AreNotSame(before, after, "предусловие: смена радиуса ПЕРЕСТРОИЛА меш");
        Assert.IsTrue(before == null,
            "старый меш никто не держит — он обязан быть уничтожен в момент замены");
    }

    /// <summary>Обратная половина контракта: базовый <c>OnDestroy</c> обязан
    /// позвать хук наследника. Лампа заводит СОБСТВЕННЫЙ материал плафона — он
    /// не дочерний объект и вместе с GameObject не умирает; его убирает только
    /// <c>LightSourceElement.OnElementDestroyed</c>. Материал уходит через
    /// <c>Destroy</c>, а тот в Play mode отложен до конца кадра — отсюда
    /// <c>yield return null</c>.</summary>
    [UnityTest]
    public IEnumerator LightSource_Destroyed_TheHookRuns_AndItsPlafondMaterialIsDestroyed()
    {
        var lamp = Spawn(ElementFactory.CreateLightSource("Лампа", new Vector3(0f, 2f, 0f)));
        var renderer = lamp.GetComponent<MeshRenderer>();
        Assert.IsTrue(renderer != null, "предусловие: у плафона есть свой рендерер");

        var material = renderer!.sharedMaterial;
        Assert.IsTrue(material != null, "предусловие: лампа создала себе материал плафона");

        Object.DestroyImmediate(lamp);
        _spawned.Remove(lamp);
        yield return null;

        Assert.IsTrue(material == null,
            "материал плафона пережил лампу: значит базовый OnDestroy не позвал "
            + "OnElementDestroyed — и вся уборка наследников исчезла разом");
    }

    /// <summary>Ради чего правило вообще существует: снятие с учёта делает ОДИН
    /// базовый OnDestroy, и оно обязано работать у наследников, чья собственная
    /// уборка живёт в хуке.</summary>
    [Test]
    public void EveryKindOfElement_Destroyed_LeavesNothingBehindInThePartRegistry()
    {
        var made = new[]
        {
            ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Деталь", Vector3.zero),
            ElementFactory.CreatePillar(PillarElement.MidHeightMM_Default, "Опора", Vector3.zero),
            ElementFactory.CreateRadialShelf(600, 400, 18, 100, "Полка", Vector3.zero),
            ElementFactory.CreateSink("Мойка", Vector3.zero),
            ElementFactory.CreateOven("Духовка", Vector3.zero),
            ElementFactory.CreateDishwasher("ПММ", Vector3.zero),
            ElementFactory.CreateLightSource("Лампа", Vector3.zero),
        };

        Assert.GreaterOrEqual(PartRegistry.All.Count, made.Length,
            "предусловие: каждый созданный элемент встал на учёт");

        foreach (var go in made) Object.DestroyImmediate(go);

        Assert.AreEqual(0, PartRegistry.All.Count,
            "в реестре повис уничтоженный элемент: снятие с учёта делает базовый OnDestroy, "
            + "и наследник, объявивший своё сообщение поверх, снова его отключил");
    }
}
