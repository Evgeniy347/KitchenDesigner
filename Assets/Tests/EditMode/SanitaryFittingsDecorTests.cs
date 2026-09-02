using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Смеситель и душевая стойка красят себя САМИ — по той же причине,
/// что и ванна (BathtubDecorTests), и потому проверяются тем же двусторонним
/// свойством. Причина стоит того, чтобы повторить её здесь: общий путь
/// MaterialManager.Apply для элемента без своей ветки пишет материал ПРЯМО В
/// РЕНДЕРЕР, мимо элемента, а умолчание каталога — серый ЛДСП. Смеситель
/// хромированный, стойка чёрная матовая, и оба умолчания живут в самих
/// элементах, поэтому серыми они выходили бы не при создании, а у КОПИИ
/// (дублирование зовёт CopyMaterial → ApplyById) и после открытия файла
/// (SceneRestorer применяет materialId каждому подряд).
///
/// Двустороннесть здесь не формальность. Заглушив первую половину — вернув
/// заводской металл всегда — получили бы сантехнику, которую нельзя
/// перекрасить, и обе половины поодиночке выглядели бы зелёными.
///
/// Третья проверка — перестройка: у труб меш собирается заново на КАЖДУЮ
/// правку любого размера (длина излива, длина шланга), то есть на порядок
/// чаще, чем у мебели. Если решение о материале принимается заново без
/// оглядки на выбор пользователя, декор слетает на первом же движении
/// ползунка, а не когда-нибудь.</summary>
public class SanitaryFittingsDecorTests
{
    private const string DecorId = "test_fitting_decor";

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

    private static KitchenElement NewMixer()
    {
        var go = ElementFactory.CreateBathMixer(BathMixerSpec.Default, "Смеситель", Vector3.zero);
        var mixer = go.GetComponent<BathMixerElement>();
        Assert.IsNotNull(mixer, "фабрика обязана вернуть объект с BathMixerElement");
        return mixer!;
    }

    private static KitchenElement NewColumn()
    {
        var go = ElementFactory.CreateShowerColumn(ShowerColumnSpec.Default, "Стойка",
            Vector3.zero);
        var column = go.GetComponent<ShowerColumnElement>();
        Assert.IsNotNull(column, "фабрика обязана вернуть объект с ShowerColumnElement");
        return column!;
    }

    private static Material? PaintOf(KitchenElement element)
    {
        var renderer = element.GetComponent<MeshRenderer>();
        Assert.IsNotNull(renderer, "элемент строит свой меш на самом объекте — рендерер "
            + "обязан лежать там же, иначе DecorRenderer указывает в пустоту");
        return renderer!.sharedMaterial;
    }

    [Test]
    public void ANewBathMixer_IsChrome_NotTheFurnitureDefault()
    {
        Assert.AreSame(SanitaryMaterials.Chrome, PaintOf(NewMixer()),
            "смеситель рождается хромированным: умолчание каталога — серый ЛДСП, это "
            + "декор мебели, а не латунная арматура");
    }

    [Test]
    public void ANewShowerColumn_IsMatteBlack_NotTheFurnitureDefault()
    {
        Assert.AreSame(SanitaryMaterials.MatteBlack, PaintOf(NewColumn()),
            "стойка с референса чёрная матовая, и это её заводской вид, а не выбор "
            + "пользователя");
    }

    [Test]
    public void ApplyingTheCatalogDefault_LeavesTheBathMixerChrome()
    {
        var mixer = NewMixer();

        MaterialManager.ApplyById(mixer, MaterialCatalog.DefaultId);

        Assert.AreSame(SanitaryMaterials.Chrome, PaintOf(mixer),
            "умолчательный id значит «пользователь ничего не выбирал», а не «покрась "
            + "серым»: через этот вызов ходят дублирование и загрузка проекта");
    }

    [Test]
    public void ApplyingTheCatalogDefault_LeavesTheShowerColumnBlack()
    {
        var column = NewColumn();

        MaterialManager.ApplyById(column, MaterialCatalog.DefaultId);

        Assert.AreSame(SanitaryMaterials.MatteBlack, PaintOf(column),
            "та же половина свойства для стойки: копия чёрной стойки обязана остаться "
            + "чёрной");
    }

    [Test]
    public void AChosenDecor_ReplacesTheFactoryChromeOfTheBathMixer()
    {
        var mixer = NewMixer();

        MaterialManager.ApplyById(mixer, DecorId);

        Assert.AreNotSame(SanitaryMaterials.Chrome, PaintOf(mixer),
            "вето на умолчание не должно превратиться в смеситель, который нельзя "
            + "перекрасить: чёрный или золотой смеситель — законный выбор");
        Assert.AreEqual(DecorId, mixer.MaterialId, "выбор обязан ещё и сохраниться");
    }

    [Test]
    public void AChosenDecor_ReplacesTheFactoryBlackOfTheShowerColumn()
    {
        var column = NewColumn();

        MaterialManager.ApplyById(column, DecorId);

        Assert.AreNotSame(SanitaryMaterials.MatteBlack, PaintOf(column),
            "вторая половина для стойки: хромированная стойка вместо чёрной — законный "
            + "выбор, и вето на умолчание не должно его отнимать");
        Assert.AreEqual(DecorId, column.MaterialId, "выбор обязан ещё и сохраниться");
    }

    [Test]
    public void AChosenDecor_SurvivesTheRebuildCausedByASpoutChange()
    {
        var mixer = (BathMixerElement)NewMixer();
        MaterialManager.ApplyById(mixer, DecorId);
        var chosen = PaintOf(mixer);

        mixer.SpoutLengthMM = BathMixerSpec.DefaultSpoutLengthMM + 40;

        Assert.AreSame(chosen, PaintOf(mixer),
            "меш труб пересобирается на каждую правку размера, и решение о материале "
            + "принимается там же: выбранный декор обязан пережить это, а не слетать "
            + "на первом же движении ползунка");
    }

    [Test]
    public void AChosenDecor_SurvivesTheRebuildCausedByAHoseChange()
    {
        var column = (ShowerColumnElement)NewColumn();
        MaterialManager.ApplyById(column, DecorId);
        var chosen = PaintOf(column);

        column.HoseLengthMM = ShowerColumnSpec.DefaultHoseLengthMM + 200;

        Assert.AreSame(chosen, PaintOf(column),
            "длина шланга меняет ломаную петли и, значит, весь меш стойки — самый "
            + "частый повод к перестройке из всех");
    }
}
