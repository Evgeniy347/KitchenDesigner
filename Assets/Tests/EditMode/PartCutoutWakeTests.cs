using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

/// <summary>F2. Уснувшие мойка и варочная перестали замечать мир.
///
/// После работы по производительности гость гасит себя (<c>enabled = false</c>),
/// когда его <c>PoseVersion</c> устоялся, а разбудить его могли только две вещи:
/// собственный сдвиг (<c>OnOwnPoseVersionBumped</c>) и <c>BumpPoseVersion</c>
/// хозяина, у которого гость УЖЕ в списке вырезов. Оба сигнала приходят от того,
/// с кем связь уже установлена, — а именно её отсутствие и надо заметить:
///
/// * удалили хозяина — гость остаётся спать с протухшей посадкой;
/// * подвинули НОВУЮ столешницу под уже стоящую варочную — её нет в списке
///   вырезов нового хозяина, значит некому её будить, и она не цепляется,
///   хотя до гашения <c>Update</c> ловил это каждый кадр.
///
/// Точка пробуждения одна: <c>SceneChangeTracker.Poll</c> уже знает и про смену
/// состава сцены (<c>_membershipChanged</c>), и про то, кто сдвинулся. Первые два
/// теста щупают именно её: в EditMode <c>Update</c> сам не идёт, поэтому судим по
/// флагу <c>enabled</c>, по которому <c>Update</c> и решает, работать ли ему.
/// Третий зовёт <c>Update</c> руками и спрашивает обоих гостей одно и то же.</summary>
public class PartCutoutWakeTests
{
    private ProjectLoadStateGuard? _globals;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        _globals = ProjectLoadStateGuard.Capture();
        CommandStack.Clear();
        EveryElementType.ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        CommandStack.Clear();
        EveryElementType.ClearScene();
        _globals?.Restore();
        LogAssert.ignoreFailingMessages = false;
    }

    private static (KitchenElement top, CooktopElement cooktop) SleepingCooktopOverItsOwnTop()
    {
        var topGo = ElementFactory.CreatePart(new Vector3Int(1200, 40, 600), "Столешница",
            new Vector3(2f, 0f, 0f));
        var top = topGo.GetComponent<KitchenElement>();
        PartRegistry.Register(top);

        var cooktopGo = ElementFactory.CreateCooktop("Варочная", new Vector3(2f, 0.02f, 0f));
        var cooktop = cooktopGo.GetComponent<CooktopElement>();
        PartRegistry.Register(cooktop);
        cooktop.SnapToPart();

        cooktop.enabled = false;
        return (top, cooktop);
    }

    [Test]
    public void ASleepingGuest_WakesWhenTheSceneMembershipChanges()
    {
        var (_, cooktop) = SleepingCooktopOverItsOwnTop();
        Assert.IsFalse(cooktop.enabled, "варочная уснула — именно это состояние и проверяем");

        SceneChangeTracker.NoteMembershipChanged();
        SceneChangeTracker.Poll();

        Assert.IsTrue(cooktop.enabled,
            "состав сцены изменился (кто-то пришёл или ушёл) — гость обязан проснуться и "
            + "пересчитать посадку: связи с новым хозяином у него ещё нет, и разбудить его "
            + "больше некому");
    }

    [Test]
    public void ASleepingGuest_WakesWhenAPartThatCouldHostItMoves()
    {
        var (_, cooktop) = SleepingCooktopOverItsOwnTop();

        var newTopGo = ElementFactory.CreatePart(new Vector3Int(1200, 40, 600), "Новая столешница",
            new Vector3(6f, 0f, 0f));
        var newTop = newTopGo.GetComponent<KitchenElement>();
        PartRegistry.Register(newTop);

        SceneChangeTracker.Poll();
        cooktop.enabled = false;
        newTop.transform.hasChanged = false;

        newTop.transform.position = new Vector3(2f, 0f, 0f);
        SceneChangeTracker.Poll();

        Assert.IsTrue(cooktop.enabled,
            "новую столешницу подвинули под спящую варочную — варочной НЕТ в списке вырезов "
            + "этой доски, поэтому BumpPoseVersion хозяина её не будит. Будить обязана смена "
            + "положения любой детали, которая МОГЛА БЫ стать хозяином");
    }

    /// <summary>Одна модель устаревания на ОБОИХ: <c>CooktopElement.Update</c>
    /// опрашивал <c>Mount.PartMoved</c>, а <c>SinkElement.Update</c> — нет, и у
    /// одного базового класса было два разных ответа на вопрос «пора ли
    /// проснуться». Вопрос задаётся поведением, а не отражением: хозяин уехал,
    /// счётчик позы не двигали (его двигает <c>SceneChangeTracker</c>, и тут он
    /// намеренно не зван) — единственный, кто может это заметить, это опрос
    /// <c>PartMoved</c> внутри <c>Update</c>. Варочная замечала и до правки,
    /// мойка — нет; теперь обе, потому что <c>Update</c> у них общий.</summary>
    [Test]
    public void BothGuests_NoticeTheHostMoved_WithNoPoseBumpAtAll()
    {
        foreach (bool asSink in new[] { false, true })
        {
            EveryElementType.ClearScene();

            var topGo = ElementFactory.CreatePart(new Vector3Int(1200, 40, 600), "Столешница",
                new Vector3(2f, 0f, 0f));
            var top = topGo.GetComponent<KitchenElement>();
            PartRegistry.Register(top);

            var guestGo = asSink
                ? ElementFactory.CreateSink("Мойка", new Vector3(2f, 0.02f, 0f))
                : ElementFactory.CreateCooktop("Варочная", new Vector3(2f, 0.02f, 0f));
            var guest = guestGo.GetComponent<PartCutoutElement>();
            PartRegistry.Register(guest);
            guest.SnapToPart();
            Assert.IsTrue(guest.IsAttached, $"предусловие ({guest.DisplayTypeName}): гость сел");

            guest.Update();
            Assert.IsFalse(guest.enabled, $"предусловие ({guest.DisplayTypeName}): гость уснул");

            float xBefore = guest.transform.position.x;
            top.transform.position += new Vector3(0.3f, 0f, 0f);
            guest.enabled = true;
            guest.Update();

            Assert.AreEqual(xBefore + 0.3f, guest.transform.position.x, 1e-3f,
                $"{guest.DisplayTypeName}: хозяин уехал на 300 мм без единого бампа позы — "
                + "заметить это может только опрос Mount.PartMoved в Update, и он обязан быть "
                + "один на всех наследников PartCutoutElement");
        }
    }
}
