using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core;

/// <summary>Один признак «высота фиксирована» — <see cref="FixedSize.IsHeightFixed"/>.
/// До него у дивана было пять независимых читателей (строка панели, ручки растяжки,
/// проволочный отказ, создание агентом, нормализация), и каждый знал про диван по
/// типу: шестой тип с фиксированной высотой забыл бы кого-нибудь из пяти. Здесь
/// закреплена сама граница признака и то, что проволочный отказ идёт за ним, а не
/// за списком имён типов.</summary>
public class FixedHeightPredicateTests : McpTestFixture
{
    [Test]
    public void IsHeightFixed_IsTrueForASofa_AndFalseForAStool()
    {
        var sofa = ElementFactory.CreateSofa(new UnityEngine.Vector3Int(1800, SofaLayout.OverallHeightMM, 900),
            SofaElement.DefaultCornerRadiusMM, SofaElement.DefaultSeatHeightMM, "FixedHeightSofa",
            UnityEngine.Vector3.zero).GetComponent<KitchenElement>();
        var stool = ElementFactory.CreateStool(new UnityEngine.Vector3Int(360, 450, 360), 20,
            "FixedHeightStool", UnityEngine.Vector3.zero).GetComponent<KitchenElement>();
        _spawned.Add(sofa.gameObject);
        _spawned.Add(stool.gameObject);

        Assert.IsTrue(FixedSize.IsHeightFixed(sofa),
            "высота дивана задана раскладкой (низ спинки + высота спинки), а не заказчиком");
        Assert.IsFalse(FixedSize.IsHeightFixed(stool),
            "у табуретки высота обычный размер: признак не должен быть «всем сразу»");
    }

    [Test]
    public void IsHeightFixed_OfNothing_IsFalse()
        => Assert.IsFalse(FixedSize.IsHeightFixed(null), "нет элемента — нечего фиксировать");

    [Test]
    public void EveryElementWhoseHeightIsFixed_RefusesAHeightEditOnTheWire()
    {
        var notRefused = new List<string>();
        var checkedTypes = 0;
        foreach (var (type, _) in EveryElementType.Makers)
        {
            EveryElementType.ClearScene();
            var el = EveryElementType.Spawn(type, "FixedProbe");
            _spawned.Add(el.gameObject);
            if (!PartRegistry.GetAll().Contains(el)) PartRegistry.Register(el);
            if (!FixedSize.IsHeightFixed(el)) continue;
            checkedTypes++;

            var resp = _handler!.Handle(MakeReq("edit_elements", new
            {
                ops = new object[] { new { name = el.PartName, height = 777 } },
            }));
            if (resp.type != "error"
                || !JObject.FromObject(resp.data!)["message"]!.ToString().Contains("height"))
                notRefused.Add(type.Name);
        }

        Assert.Greater(checkedTypes, 0, "хотя бы диван обязан быть среди типов с фиксированной высотой");
        Assert.IsEmpty(notRefused,
            "тип, у которого признак говорит «высота фиксирована», обязан получить отказ при правке "
            + "height по проводу: " + string.Join(", ", notRefused));
        EveryElementType.ClearScene();
    }
}
