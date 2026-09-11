using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Наведение на пункт списка «Прикрепить к» обязано показать в сцене ту
/// деталь, которую пункт НАЗЫВАЕТ, — полным прокрасом поверх всего, как рисуются
/// накладки: деталь, закрытая соседями, иначе не подсветится вовсе, и пользователь
/// выбирает родителя вслепую.
///
/// Красится не «первый рендерер», а всё тело (<see cref="ElementRenderers.BodyOf"/>):
/// у табуретки меша на корне нет вовсе, и <c>GetComponent&lt;MeshRenderer&gt;()</c>
/// покрасил бы пустоту. Поэтому подопытный здесь составной, а свидетель — обычная
/// доска: «покрасили ту деталь» и «не покрасили соседнюю» — два разных вопроса, и
/// второй ловит прокрас по площадям.
///
/// Возврат проверяется ПОРЕНДЕРНО и по ССЫЛКЕ на исходный материал, а последний
/// тест задаёт возврату тот вопрос, на котором он ломается молча: чтение
/// <c>renderer.material</c> — это мутация, Unity подменяет материал копией
/// (`conventions/CORRECTNESS.md` → «Reading `renderer.material` is a MUTATION»), и
/// возврат, узнающий свою краску по АДРЕСУ, принимает копию за чужой материал и
/// оставляет зелёное навсегда.
///
/// Противоположный вход — пункт «(не прикреплено)»: он не называет ни одной детали,
/// и сцена обязана остаться нетронутой. Без него тест «красит по наведению» зеленел
/// бы и от кода, который красит на ЛЮБОЕ наведение.</summary>
public class AttachTargetHoverTests
{
    private const string NoneCaption = "(не прикреплено)";

    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        HoverPreviewGate.HideAll();
        CommandStack.Clear();
        foreach (var element in PartRegistry.GetAll())
            if (element != null) _spawned.Add(element.gameObject);
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        ElementFactory.ClearPools();
    }

    private KitchenElement Stool(string name)
    {
        var go = ElementFactory.CreateStool(new Vector3Int(360, 450, 360), 0, name, Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private KitchenElement Board(string name)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 400), name,
            new Vector3(2f, 0f, 0f));
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private AttachTargetHover HoverOver(params string[] names)
    {
        UIFactory.EnsureEventSystem();
        var canvas = UIFactory.CreateCanvas("AttachHoverCanvas");
        _spawned.Add(canvas.gameObject);

        var captions = new List<string> { NoneCaption };
        captions.AddRange(names);
        var dropdown = UIFactory.CreateDropdown("CtxAttachTo", canvas.transform, captions,
            Vector2.zero, new Vector2(200, 28), _ => { });
        return AttachTargetHover.Watch(dropdown, NoneCaption);
    }

    private static List<Material> MaterialsOf(KitchenElement element)
    {
        var materials = new List<Material>();
        foreach (var renderer in ElementRenderers.BodyOf(element))
            materials.Add(renderer == null ? null! : renderer.sharedMaterial);
        return materials;
    }

    private static List<string> NotWearingTheHoverTint(KitchenElement element)
    {
        var missed = new List<string>();
        foreach (var renderer in ElementRenderers.BodyOf(element))
        {
            if (renderer == null) continue;
            var material = renderer.sharedMaterial;
            if (material == null) continue;
            if (material.name.StartsWith(ElementTint.HoverName, System.StringComparison.Ordinal))
                continue;
            missed.Add(ElementRenderers.PathOf(element, renderer));
        }
        return missed;
    }

    private static List<string> MaterialsThatMoved(KitchenElement element, List<Material> before)
    {
        var moved = new List<string>();
        var body = ElementRenderers.BodyOf(element);
        for (int i = 0; i < body.Count && i < before.Count; i++)
        {
            if (body[i] == null) continue;
            if (ReferenceEquals(body[i].sharedMaterial, before[i])) continue;
            moved.Add(ElementRenderers.PathOf(element, body[i]));
        }
        return moved;
    }

    [Test]
    public void HoveringAnItem_PaintsThePartItNames_WholeAndNothingElse()
    {
        var named = Stool("Табурет");
        var bystander = Board("Доска");
        var bystanderBefore = MaterialsOf(bystander);
        var hover = HoverOver(named.PartName, bystander.PartName);

        Assume.That(ElementRenderers.BodyOf(named).Count, Is.GreaterThan(1),
            "подопытный обязан быть составным — на одном рендерере «красим тело целиком» "
            + "неотличимо от «красим первый попавшийся»");

        hover.Enter(1);

        Assert.AreSame(named, HoverTint.ShownFor,
            "подсветка обязана встать на деталь, которую НАЗЫВАЕТ пункт");
        Assert.IsEmpty(NotWearingTheHoverTint(named),
            "прокрас обязан накрыть тело целиком (ElementRenderers.BodyOf), а не корневой меш");
        Assert.Greater(HoverTint.PaintedRendererCount, 1,
            "рендерер без материала выводить не из чего, и скан их пропускает — "
            + "без этой проверки пустой обход позеленел бы на любом коде");
        Assert.IsEmpty(MaterialsThatMoved(bystander, bystanderBefore),
            "соседняя деталь не названа пунктом — её материал трогать нельзя");
    }

    [Test]
    public void LeavingTheItem_GivesEveryRendererItsOwnMaterialBack()
    {
        var named = Stool("Табурет");
        var before = MaterialsOf(named);
        var hover = HoverOver(named.PartName);

        hover.Enter(1);
        Assume.That(NotWearingTheHoverTint(named), Is.Empty,
            "до ухода курсора прокрас обязан лежать — иначе возврат проверяется вхолостую");

        hover.Leave();

        Assert.IsEmpty(MaterialsThatMoved(named, before),
            "уход с пункта возвращает КАЖДОМУ рендереру его собственный материал");
        Assert.IsNull(HoverTint.ShownFor, "и снимает подсветку целиком");
        Assert.AreEqual(0, HoverTint.PaintedRendererCount,
            "оставшаяся запись о прокрасе — это материал, который некому вернуть");
    }

    [Test]
    public void HoveringTheNoneItem_PaintsNothingAtAll()
    {
        var candidate = Stool("Табурет");
        var before = MaterialsOf(candidate);
        var hover = HoverOver(candidate.PartName);

        hover.Enter(0);

        Assert.IsNull(HoverTint.ShownFor,
            "пункт «" + NoneCaption + "» не называет детали — красить нечего");
        Assert.AreEqual(0, HoverTint.PaintedRendererCount, "и ни один рендерер не перекрашен");
        Assert.IsEmpty(MaterialsThatMoved(candidate, before),
            "деталь, которая просто есть в списке, подсветки не получает");
    }

    [Test]
    public void LeavingTheItem_GivesTheMaterialBack_EvenAfterSomethingReadRendererMaterial()
    {
        var named = Stool("Табурет");
        var before = MaterialsOf(named);
        var hover = HoverOver(named.PartName);

        hover.Enter(1);

        bool copied = false;
        foreach (var renderer in ElementRenderers.BodyOf(named))
        {
            if (renderer == null) continue;
            var painted = renderer.sharedMaterial;
            var copy = renderer.material;
            if (painted != null && !ReferenceEquals(copy, painted)) copied = true;
        }

        Assume.That(copied, Is.True,
            "чтение renderer.material обязано подменить материал копией — иначе этот тест "
            + "проверяет не ту ситуацию, ради которой написан");

        hover.Leave();

        Assert.IsEmpty(MaterialsThatMoved(named, before),
            "свою краску возврат обязан узнавать по ИМЕНИ, а не по адресу: копия нашей "
            + "краски — это наша краска, и материал обязан вернуться");
    }
}
