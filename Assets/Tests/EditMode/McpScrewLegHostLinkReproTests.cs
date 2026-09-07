using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Второй вход в то же поведение: позицию элемента меняет не мышь, а
/// <c>edit_elements</c>. Связь опоры с хозяином выведена из геометрии, поэтому она
/// обязана пересчитаться и здесь — и пересчитать её обязана ТА ЖЕ функция, что и
/// в кадровом опросе (<c>SceneChangeTracker.SettleDerivedLinks</c>), а не отдельная
/// ветка внутри обработчика: см. CONVENTIONS.md → «Not only the guard — every READER
/// of a state must ask through one function».
///
/// Пара к <see cref="ScrewLegHostLinkReproTests"/>: там тот же сценарий проходит
/// мышиной дорогой.</summary>
public class McpScrewLegHostLinkReproTests : McpTestFixture
{
    private const float U = AppConstants.MM_TO_UNITS;

    private static JObject Data(McpResponse resp) => JObject.FromObject(resp.data!);

    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    private void Edit(object op)
    {
        var resp = _handler!.Handle(MakeReq("edit_elements", new { ops = new[] { op } }));
        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
    }

    private KitchenElement BottomPanel(string name, float xMM, float bottomMM) =>
        MakeElement(name, new Vector3Int(600, 18, 500),
            new Vector3(xMM * U, (bottomMM + 9f) * U, 0f));

    /// <summary>Опора уже вкрученная в левое дно: резьба 167 мм + пятка 8 мм, пятка
    /// на полу, конец резьбы на 175 мм — то есть 25 мм внутри дна, стоящего на 150.</summary>
    private ScrewLegElement SeatedLeg()
    {
        var go = ElementFactory.CreateScrewLeg("Опора1", new Vector3(0f, 0.0875f, 0f));
        _spawned.Add(go);
        var leg = go.GetComponent<ScrewLegElement>();
        leg.ThreadLengthMM = 167;
        leg.transform.position = new Vector3(0f, 0.0875f, 0f);
        ScrewLegHostLink.Apply(leg, PartRegistry.GetAll());
        return leg;
    }

    private (KitchenElement left, KitchenElement right, ScrewLegElement leg) Scene()
    {
        var left = BottomPanel("ДноЛевое", 0f, 150f);
        var right = BottomPanel("ДноПравое", 1000f, 160f);
        var leg = SeatedLeg();

        Assume.That(leg.HostPartName, Is.EqualTo("ДноЛевое"), "исходная посадка");
        Assume.That(leg.InsertionIntoHostMM, Is.EqualTo(25), "исходный заход — 25 мм");
        return (left, right, leg);
    }

    /// <summary>Опора Ø25, поэтому минимальный угол стоит на 12,5 мм левее центра.</summary>
    private static float AnchorXForCentre(ScrewLegElement leg, float centreMM) =>
        centreMM - leg.DimensionsMM.x * 0.5f;

    [Test]
    public void EditElements_LegMovedUnderAnotherBoard_TakesThatBoardAsItsHost()
    {
        var (_, right, leg) = Scene();

        Edit(new { name = leg.PartName, anchor_x_mm = AnchorXForCentre(leg, 1000f) });

        Assume.That(leg.transform.position.x, Is.EqualTo(1f).Within(1e-4f),
            "предусловие: опора действительно встала под правое дно");
        Assert.AreEqual(right.PartName, leg.HostPartName,
            "мутация позиции через MCP — такой же переезд опоры, как мышиный");
        Assert.AreEqual(15, leg.InsertionIntoHostMM,
            "резьба кончается на 175 мм, дно правого корпуса на 160 мм");
    }

    [Test]
    public void EditElements_LegMovedUnderAnotherBoard_ReportsTheNewHostInTheSameSession()
    {
        var (_, _, leg) = Scene();

        Edit(new { name = leg.PartName, anchor_x_mm = AnchorXForCentre(leg, 1000f) });
        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { leg.PartName } }));
        var info = Data(resp)["elements"]![0]!["screwLeg"]!;

        Assert.AreEqual("ДноПравое", info["hostName"]!.Value<string>(),
            "агент читает ту же величину, что и панель, и обязан увидеть её сразу "
            + "после собственной мутации, а не через кадр");
        Assert.AreEqual(15, info["insertionMM"]!.Value<int>());
    }

    [Test]
    public void EditElements_LegMovedOutFromUnderEverything_ReportsNoHostAndZeroInsertion()
    {
        var (_, _, leg) = Scene();

        Edit(new { name = leg.PartName, anchor_x_mm = AnchorXForCentre(leg, 3000f) });

        Assert.IsNull(leg.HostPartName, "над опорой не осталось деталей");
        Assert.IsNull(leg.InsertionIntoHostMM,
            "прочерк, а не последнее живое число: 25 мм здесь означало бы мерку "
            + "по детали, до которой резьбе теперь два метра");
    }

    [Test]
    public void EditElements_HostShrunkAwayFromTheLeg_ClearsTheLegsLink()
    {
        var (_, _, leg) = Scene();
        Edit(new { name = leg.PartName, anchor_x_mm = AnchorXForCentre(leg, 200f) });
        Assume.That(leg.HostPartName, Is.EqualTo("ДноЛевое"), "200 мм ещё под деталью 600 мм");

        Edit(new { name = "ДноЛевое", width = 100 });

        Assert.IsNull(leg.HostPartName,
            "двигают не опору, а деталь над ней: хозяин сузился и ушёл из-под резьбы — "
            + "связь обязана очиститься");
        Assert.IsNull(leg.InsertionIntoHostMM);
    }
}
