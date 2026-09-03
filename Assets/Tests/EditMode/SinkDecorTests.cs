using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Заводская сталь мойки обязана пережить <c>MaterialManager.Apply</c>,
/// и выбранный декор обязан пережить его тоже.
///
/// Дефект тот же, что у духовки и посудомойки. Общий путь <c>Apply</c> для
/// элемента без своей ветки пишет материал прямо в рендерер, мимо элемента, а
/// умолчание каталога — серый ЛДСП. Мойка строится от <c>ElementRoot.NewEmpty</c>,
/// рендерера на корне нет, и <c>GetComponentInChildren&lt;MeshRenderer&gt;()</c>
/// находит первого ребёнка — «RimFront». Ходят туда открытие проекта
/// (<c>ElementRestorers.ApplyShared</c> → <c>ApplyById</c>) и дублирование
/// (<c>CopyMaterial</c> → тот же <c>ApplyById</c>): один борт бортика становился
/// серым у копии и после загрузки файла, а остальная мойка оставалась стальной.
///
/// Обратная сторона: выбранный декор ложился ровно на этого одного ребёнка.
/// Поэтому проверка декора смотрит на «RimBack», а не только на «RimFront»: на
/// первого ребёнка декор ложился и БЕЗ правки, и тест по нему одному зеленел бы,
/// ничего не доказав.
///
/// Лечится способностью <c>IPaintsItself</c>, а не веткой по типу в
/// MaterialManager (CONVENTIONS.md → «Element type checks live in ONE place per
/// layer»). Декор кладётся на бортик и чашу; смеситель остаётся стальным.
///
/// Свойство ДВУСТОРОННЕЕ, и односторонний тест был бы хуже, чем никакого:
/// заглушив первое возвратом стали всегда, получили бы мойку, которую нельзя
/// перекрасить.
///
/// Про <c>AChosenDecor_SurvivesARebuildCausedByResizing</c> отдельно. Сначала он
/// сравнивал материал «RimBack» ПОСЛЕ перестройки с материалом того же борта ДО
/// неё — и на сломанном коде вышел зелёным, хотя обязан был краснеть: декор туда
/// не попадал, «до» и «после» оба были заводской сталью, и упасть тест не мог в
/// принципе. Перестройка при этом честно работает — <c>SinkMesh.Rebuild</c>
/// заканчивается <c>ApplyMaterials</c>. Теперь сравнение идёт с материалом декора
/// из каталога, а перед перестройкой на борт нарочно мажется третий материал: не
/// запустись перестройка — мазок доживёт до ассерта и тест покраснеет.
///
/// Второй путь перестройки у мойки — <c>UpdateFaucetSide</c>: он тоже зовёт
/// <c>SinkMesh.Rebuild</c> и тоже смывал бы декор, поэтому решение про материал
/// принимается внутри <c>SinkElement.Skin</c>, общего для обоих вызовов, а не в
/// точке вызова.</summary>
public class SinkDecorTests
{
    private const string DecorId = "test_sink_decor";

    private const string RimFront = "RimFront";
    private const string RimBack = "RimBack";
    private const string BowlLeft = "BowlLeft";
    private const string FaucetStand = "FaucetStand";

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        MaterialCatalog.Register(new MaterialDef(DecorId, "Тестовый декор", "плитка",
            new Color(0.2f, 0.4f, 0.6f)));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var el in new List<KitchenElement>(PartRegistry.GetAll()))
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        ElementFactory.ClearPools();
        MaterialCatalog.Reset();
    }

    private static SinkElement NewSink()
    {
        var go = ElementFactory.CreateSink("Мойка", Vector3.zero);
        var sink = go.GetComponent<SinkElement>();
        Assert.IsNotNull(sink, "фабрика обязана вернуть объект с SinkElement");
        return sink!;
    }

    private static MeshRenderer RendererOf(SinkElement sink, string childName)
    {
        var part = sink.transform.Find(childName);
        Assert.IsNotNull(part, "мойка строит деталь «" + childName + "» отдельным "
            + "ребёнком — без него тест смотрит в пустоту и зеленеет по недоразумению");
        var renderer = part!.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, "у детали «" + childName + "» обязан быть рендерер: "
            + "именно такого ребёнка находит общий путь Apply, когда у элемента нет "
            + "своей ветки");
        return renderer!;
    }

    private static Material? PaintOf(SinkElement sink, string childName)
        => RendererOf(sink, childName).sharedMaterial;

    private static Material ChosenDecor()
    {
        var mat = MaterialManager.GetSharedMaterial(MaterialCatalog.Get(DecorId));
        Assert.IsNotNull(mat, "каталог обязан отдать материал зарегистрированного декора — "
            + "иначе сравнивать не с чем и тест зеленеет впустую");
        return mat!;
    }

    [Test]
    public void ANewSink_IsFactorySteel_NotTheFurnitureDefault()
    {
        var sink = NewSink();

        Assert.AreSame(ApplianceMaterials.SinkSteel, PaintOf(sink, RimFront),
            "мойка рождается стальной: умолчание каталога — серый ЛДСП, и это декор "
            + "мебели, а не техники");
    }

    [Test]
    public void ApplyingTheCatalogDefault_LeavesTheSteelAlone()
    {
        var sink = NewSink();

        MaterialManager.ApplyById(sink, MaterialCatalog.DefaultId);

        Assert.AreSame(ApplianceMaterials.SinkSteel, PaintOf(sink, RimFront),
            "умолчательный id значит «пользователь ничего не выбирал», а не «покрась "
            + "серым». Через этот вызов ходят открытие проекта (ApplyShared) и "
            + "дублирование (CopyMaterial), поэтому один борт бортика серел после "
            + "загрузки файла и у копии");
    }

    [Test]
    public void ACopiedSink_KeepsTheFactorySteel()
    {
        var copy = ElementFactory.Duplicate(NewSink()).GetComponent<SinkElement>();
        Assert.IsNotNull(copy, "дублирование обязано вернуть объект с SinkElement");

        Assert.AreSame(ApplianceMaterials.SinkSteel, PaintOf(copy!, RimFront),
            "копия проходит CopyMaterial с умолчательным id оригинала — путь, на котором "
            + "заводской вид теряется молча");
    }

    [Test]
    public void AChosenDecor_CoversTheWholeBowl_NotJustOneChild()
    {
        var sink = NewSink();

        MaterialManager.ApplyById(sink, DecorId);

        Assert.AreSame(ChosenDecor(), PaintOf(sink, RimBack),
            "выбранный декор обязан лечь на всю мойку. Общий путь Apply красил ровно "
            + "одного ребёнка — «RimFront», — поэтому проверять надо соседний борт: "
            + "по первому тест зеленел бы и без правки");
        Assert.AreSame(ChosenDecor(), PaintOf(sink, BowlLeft),
            "стенка чаши — часть той же поверхности, что и бортик");
        Assert.AreSame(ApplianceMaterials.SinkSteel, PaintOf(sink, FaucetStand),
            "смеситель остаётся стальным: он не отделка, а арматура");
        Assert.AreEqual(DecorId, sink.MaterialId, "выбор обязан ещё и сохраниться");
    }

    [Test]
    public void AChosenDecor_SurvivesARebuildCausedByResizing()
    {
        var sink = NewSink();
        MaterialManager.ApplyById(sink, DecorId);
        RendererOf(sink, RimBack).sharedMaterial = ApplianceMaterials.SinkBowlBottom;

        sink.DimensionsMM = new Vector3Int(SinkElement.OUTER_WIDTH_MM,
            SinkElement.TotalHeightMM, SinkElement.OUTER_DEPTH_MM);

        Assert.AreSame(ChosenDecor(), PaintOf(sink, RimBack),
            "перестройка меша решает про материал заново, и «поставь заводскую сталь» "
            + "вернула бы сталь поверх выбранного декора при первом же изменении "
            + "размера. Мазок дном чаши перед перестройкой — страховка от второй беды: "
            + "если перестройка вообще не запустится, он доживёт до сюда и тест "
            + "покраснеет, вместо того чтобы пройти впустую");
    }
}
