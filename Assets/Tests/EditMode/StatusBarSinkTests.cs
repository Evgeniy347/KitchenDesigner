#nullable disable
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Core.Update;

/// <summary>Переходник между автообновлением и статус-полосой. Единственное его
/// собственное поведение — молчать, когда полосы ещё нет.
///
/// «Полосы нет» — это глобальный статик, и раньше тест ЖДАЛ его пустым через
/// `Assume`: не совпало — Inconclusive, то есть тест ничего не проверил и не
/// пожаловался. Совпадать оно перестало сразу, как только у `StatusBarUI`
/// появился явный вход `SimulateAwakeForTests` (4bb17174): движок не считает
/// такой компонент разбуженным и не зовёт его `OnDestroy`, поэтому `Instance`
/// оставался указывать на УЖЕ УНИЧТОЖЕННУЮ полосу. NUnit-овский `Is.Null`
/// сравнивает ссылки и про уничтоженные объекты Unity ничего не знает — вот и
/// весь Inconclusive.
///
/// Лечится с двух сторон. Тест теперь САМ ставит то состояние, о котором
/// спрашивает (`SwapInstanceForTests`), и утверждает его через `Assert`, а не
/// надеется на порядок классов в прогоне. А `StatusBarUI.Instance` перестал
/// выдавать уничтоженную полосу: восемь мест в продакшене зовут
/// `Instance?.ShowTransient`, и на мёртвом объекте `?.` не спасает — это
/// `MissingReferenceException` в ответ на сообщение о сохранении. Мёртвая полоса
/// теперь и есть «полосы нет», как `PartRegistry` не отдаёт уничтоженные
/// элементы (`agents/TEST-DESIGN.md`).</summary>
public class StatusBarSinkTests
{
    private StatusBarUI _previous;
    private GameObject _go;

    [SetUp]
    public void SetUp() => _previous = StatusBarUI.SwapInstanceForTests(null);

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
        _go = null;
        StatusBarUI.SwapInstanceForTests(_previous);
    }

    private StatusBarUI LiveStatusBar()
    {
        _go = new GameObject("StatusBar");
        var bar = _go.AddComponent<StatusBarUI>();
        bar.SimulateAwakeForTests();
        Assert.AreSame(bar, StatusBarUI.Instance,
            "предусловие: EditMode не зовёт Awake сам, полосу делает текущей явный толчок");
        return bar;
    }

    [Test]
    public void Show_BeforeTheStatusBarExists_IsSilentInsteadOfThrowing()
    {
        Assert.IsNull(StatusBarUI.Instance,
            "проверяем именно случай «полосы ещё нет», и состояние задано тестом, "
            + "а не унаследовано от того, кто отработал раньше");

        var sink = new StatusBarSink();

        Assert.DoesNotThrow(() => sink.Show("обновление", StatusLevel.Info, 3f),
            "проверка обновлений стартует по таймеру через пару секунд после запуска "
            + "и обгоняет узкие места старта: StatusBarUI может быть ещё не создан. "
            + "Это одноразовая подсказка, а не событие, ради которого можно уронить "
            + "стартующее приложение");
        Assert.DoesNotThrow(() => sink.Show("сбой", StatusLevel.Error, 3f));
        Assert.DoesNotThrow(() => sink.Show("готово", StatusLevel.Success, 3f));
    }

    [Test]
    public void Show_AfterTheStatusBarWasDestroyed_IsSilentToo()
    {
        LiveStatusBar();
        Object.DestroyImmediate(_go);
        _go = null;

        Assert.IsNull(StatusBarUI.Instance,
            "уничтоженная полоса — это «полосы нет»: движок не зовёт OnDestroy у компонента, "
            + "которого не будил сам, поэтому статик остаётся заполненным, и отсеять мёртвую "
            + "ссылку обязан сам геттер");

        Assert.DoesNotThrow(() => new StatusBarSink().Show("обновление", StatusLevel.Info, 3f),
            "MissingReferenceException в ответ на подсказку об обновлении — это падение "
            + "приложения на ровном месте");
    }

    [Test]
    public void Show_WithALiveStatusBar_ReachesIt()
    {
        var bar = LiveStatusBar();

        new StatusBarSink().Show("обновление", StatusLevel.Info, 3f);

        Assert.IsTrue(bar.HasActive,
            "противоположный вход: без этой половины «молчит, когда полосы нет» зеленело бы "
            + "и на переходнике, который не показывает НИЧЕГО и никогда");
        Assert.AreEqual("обновление", bar.ActiveText);
    }
}
