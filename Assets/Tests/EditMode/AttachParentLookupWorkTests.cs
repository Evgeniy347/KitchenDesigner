using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>Четвёртый раз за две сессии одна и та же форма: «за деталь считаем по
/// всей сцене». Первые три были <c>SceneAnalyzer.CollectEdgeCover</c>,
/// <c>EdgeBanding.Coverage</c> и <c>EdgeSubstrate.Sync</c>; эта пряталась за
/// БЕЗЫМЯННЫМ путём к списку деталей и до учёта <c>PartRegistry.All</c> была не
/// видна вовсе — строка дампа говорила «обходов сцены 1».
///
/// Число из лога пользователя от 13.09: на кадре разбора сцены список деталей
/// выдавался <b>85 раз</b> (в двух кадрах — 350 и 357). Виновник —
/// <c>AttachLinks.Parent</c>: поиск родителя ПО ИМЕНИ линейным перебором всей
/// сцены, вызываемый на каждую деталь из двух фаз разбора
/// (<c>CollectAttachLinks</c> и <c>CollectScrewLegMounting</c>). 85 = примерно 61
/// прикреплённая деталь плюс 24 винтовые опоры.
///
/// Цена одного перебора на сцене в 411 деталей, разложенная по видам работы:
/// <c>other != null</c> и <c>other != e</c> — это ДВА перегруженных оператора
/// <c>UnityEngine.Object</c>, то есть два перехода в нативный код на деталь, и
/// только третья проверка сравнивает строки. На 85 поисков выходит ~70 000
/// нативных вызовов и ~35 000 сравнений строк за один разбор.
///
/// Лечение — индекс «имя → деталь», построенный ОДИН раз на фазу из списка,
/// который у разбора уже на руках: обходов списка ноль, переборов ноль.
///
/// Ловушка, из-за которой индекс обязан быть «первый по порядку регистрации»:
/// ИМЕНА В СЦЕНЕ НЕ ОБЯЗАНЫ БЫТЬ УНИКАЛЬНЫМИ. <c>ElementNaming.MakeUnique</c>
/// разводит их при СОЗДАНИИ и сравнивает БЕЗ учёта регистра, а поиск родителя
/// сравнивает С учётом; сохранённый проект тоже может принести дубли. Перебор
/// брал первого совпавшего — индекс обязан брать того же самого, а не «более
/// правильного»: это чужая связь, и она уедет в сохранённый файл. Поэтому
/// индекс хранит ПЕРВОГО по порядку и сравнивает <c>StringComparer.Ordinal</c>,
/// а на единственном случае, где перебор пропускал совпадение (деталь, чьё имя
/// совпало с её собственным <c>AttachedToName</c>), путь по индексу честно
/// сваливается обратно в перебор.</summary>
public class AttachParentLookupWorkTests : ElementTestBase
{
    private bool _suppressBefore;

    [SetUp]
    public void SetUp()
    {
        PartRegistry.Clear();
        _suppressBefore = KitchenElement.SuppressVisualRebuild;
        KitchenElement.SuppressVisualRebuild = true;
        AttachLinks.TakePartsLookedAt();
    }

    [TearDown]
    public void TearDown()
    {
        KitchenElement.SuppressVisualRebuild = _suppressBefore;
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        AttachLinks.TakePartsLookedAt();
    }

    private KitchenElement ABoard(string name, float x) =>
        MakePrimitiveElement(name, new Vector3Int(600, 720, 18), new Vector3(x, 0.36f, 0f));

    /// <summary>Пары «хозяин + прикреплённый»: прикреплённых ровно половина, и
    /// каждый из них при разборе спрашивает своего родителя по имени.</summary>
    private List<KitchenElement> APairedScene(int pairs)
    {
        var all = new List<KitchenElement>(pairs * 2);
        for (int i = 0; i < pairs; i++)
        {
            var host = ABoard("Host" + i, i * 2f);
            var rider = ABoard("Rider" + i, i * 2f + 0.02f);
            rider.AttachedToName = host.PartName;
            all.Add(host);
            all.Add(rider);
        }
        return all;
    }

    private static int PartsLookedAtInOneAnalysis()
    {
        AttachLinks.TakePartsLookedAt();
        SceneAnalyzer.Analyze();
        return AttachLinks.TakePartsLookedAt();
    }

    [Test]
    public void AnalysingTwiceTheScene_LooksAtNoMoreParts_ThanTwiceAsMany()
    {
        APairedScene(4);
        int small = PartsLookedAtInOneAnalysis();

        APairedScene(4);
        int twiceAsBig = PartsLookedAtInOneAnalysis();

        Assert.AreEqual(2 * small, twiceAsBig,
            $"перебор ради одного родителя вернулся: 8 деталей — {small} просмотров, "
            + $"16 — {twiceAsBig}. Квадратичная форма этого равенства не выдерживает, "
            + "и на 411 деталях она стоила ~70 000 нативных вызовов за разбор");
    }

    [Test]
    public void TheAnalysis_LooksAtNoPartAtAll_WhenItHasTheIndex()
    {
        APairedScene(6);

        Assert.AreEqual(0, PartsLookedAtInOneAnalysis(),
            "родитель ищется в индексе по имени; до правки на эти 12 деталей "
            + "приходилось 6 переборов по 12, а на сцене пользователя — 85 по 411");
    }

    [Test]
    public void TheLinearSearch_StillCountsWhatItLooksAt()
    {
        var all = APairedScene(3);

        AttachLinks.TakePartsLookedAt();
        AttachLinks.Parent(all[1]);

        Assert.Greater(AttachLinks.TakePartsLookedAt(), 0,
            "счётчик обязан считать: сенсор, который не может сработать, бесполезен");
    }

    [Test]
    public void TheIndex_FindsTheSameParent_AsTheLinearSearch()
    {
        var all = APairedScene(5);
        var byName = AttachLinks.PartsByName.Of(all);

        foreach (var e in all)
            Assert.AreSame(AttachLinks.Parent(e), AttachLinks.Parent(e, byName),
                $"деталь {e.PartName}: индекс назвал другого родителя");
    }

    [Test]
    public void TheIndex_TakesTheFirstOfTwoPartsSharingAName_JustAsTheSearchDid()
    {
        var first = ABoard("Twin", 0f);
        var second = ABoard("Twin", 2f);
        var rider = ABoard("Rider", 0.02f);
        rider.AttachedToName = "Twin";
        var all = new List<KitchenElement> { first, second, rider };

        var byIndex = AttachLinks.Parent(rider, AttachLinks.PartsByName.Of(all));

        Assert.AreSame(first, AttachLinks.Parent(rider),
            "перебор брал ПЕРВОГО совпавшего по имени — фикстура собрана зря, если это не так");
        Assert.AreSame(first, byIndex,
            "при дублях имён индекс обязан назвать ТОГО ЖЕ родителя, что и перебор: "
            + "это чужая связь, и смена родителя уедет в сохранённый проект");
        Assert.AreNotSame(second, byIndex);
    }

    [Test]
    public void RenamingTheParent_BreaksTheLink_AndTheNewNameRestoresIt()
    {
        var host = ABoard("Host", 0f);
        var rider = ABoard("Rider", 0.02f);
        rider.AttachedToName = "Host";
        var all = new List<KitchenElement> { host, rider };

        Assert.AreSame(host, AttachLinks.Parent(rider, AttachLinks.PartsByName.Of(all)));

        host.PartName = "Renamed";
        Assert.IsNull(AttachLinks.Parent(rider, AttachLinks.PartsByName.Of(all)),
            "индекс, построенный после переименования, не имеет права помнить старое имя");

        rider.AttachedToName = "Renamed";
        Assert.AreSame(host, AttachLinks.Parent(rider, AttachLinks.PartsByName.Of(all)),
            "и обязан найти хозяина по новому имени");
    }

    [Test]
    public void ReattachingTheChild_FindsTheOtherParent()
    {
        var one = ABoard("One", 0f);
        var two = ABoard("Two", 2f);
        var rider = ABoard("Rider", 0.02f);
        rider.AttachedToName = "One";
        var all = new List<KitchenElement> { one, two, rider };
        var byName = AttachLinks.PartsByName.Of(all);

        Assert.AreSame(one, AttachLinks.Parent(rider, byName));

        rider.AttachedToName = "Two";

        Assert.AreSame(two, AttachLinks.Parent(rider, byName),
            "перепривязка меняет ИМЯ у ребёнка, а не состав сцены — тот же индекс "
            + "обязан отдать нового родителя без пересборки");
    }

    [Test]
    public void APartWithNoAttachment_HasNoParent_AndCostsNoSearch()
    {
        var lonely = ABoard("Lonely", 0f);
        ABoard("Other", 2f);
        var all = new List<KitchenElement> { lonely };

        AttachLinks.TakePartsLookedAt();

        Assert.IsNull(AttachLinks.Parent(lonely));
        Assert.IsNull(AttachLinks.Parent(lonely, AttachLinks.PartsByName.Of(all)));
        Assert.AreEqual(0, AttachLinks.TakePartsLookedAt(),
            "деталь без привязки отсекается до перебора — так было и так осталось");
    }
}
