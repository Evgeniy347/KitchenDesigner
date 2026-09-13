using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Дефект в самом приборе, найденный в логе пользователя от 13.09:
///
/// <code>[Perf] кадр 3017: 852 мс, мусор 640 КБ, обходов сцены 15 — (без имени: 15)</code>
///
/// Пятнадцать НАЗВАННЫХ обходов, у которых имена потерялись все до одного. Кадр
/// 3017 — ПЕРВАЯ строка лога, то есть первый кадр после включения замера, и это
/// и есть объяснение: у прибора две половины, и сбрасывались они в разное время.
/// Имена живут в <c>SceneScanLog</c>, и <c>StartWindow</c> их забывал; счётчик
/// вызовов живёт в <c>PartRegistryInstance</c>, и его не сливал никто, пока замер
/// был выключен — <c>Sample</c> в это время не работает. Всё, что сцена успела
/// обойти за минуты до нажатия F9, падало одним числом в первый же измеренный
/// кадр, к имени которого оно не имело отношения.
///
/// Цена ошибки не в пятнадцати обходах, а в доверии: строка <c>(без имени: N)</c>
/// существует как СВЕРКА двух половин, и ложное срабатывание сверки учит её
/// игнорировать. Теперь она может сработать только на настоящем расхождении.
///
/// Тесты проверяют не миллисекунды, а ПОРЯДОК сброса: обе половины обнуляются
/// одним движением, и сразу после старта окна они согласны друг с другом.</summary>
public class PerfMonitorWindowStartTests
{
    private GameObject? _host;
    private PerfMonitor? _monitor;
    private bool _enabledBefore;

    [SetUp]
    public void SetUp()
    {
        _enabledBefore = PerfMonitor.Enabled;
        PerfMonitor.Enabled = false;
        PartRegistry.Clear();
        SceneScanLog.Forget();
        PartRegistryInstance.TakeGetAllCalls();
    }

    [TearDown]
    public void TearDown()
    {
        if (_monitor != null) _monitor.SimulateOnDestroyForTests();
        _monitor = null;
        if (_host != null) Object.DestroyImmediate(_host);
        _host = null;
        PerfMonitor.Enabled = _enabledBefore;
        PartRegistry.Clear();
        SceneScanLog.Forget();
        PartRegistryInstance.TakeGetAllCalls();
    }

    private void AMonitorThatIsNotMeasuringYet()
    {
        _host = new GameObject("PerfMonitor");
        _monitor = _host.AddComponent<PerfMonitor>();
        _monitor.SimulateAwakeForTests();
        PerfMonitor.Enabled = false;
    }

    private static void TheSceneIsWalked(int times)
    {
        for (int i = 0; i < times; i++) PartRegistry.GetAll();
    }

    [Test]
    public void StartingAWindow_DropsTheWalksCountedWhileNobodyWasMeasuring()
    {
        AMonitorThatIsNotMeasuringYet();
        TheSceneIsWalked(15);

        _monitor!.SimulateF9ForTests();

        Assert.IsTrue(PerfMonitor.Enabled, "F9 обязан был включить замер — иначе тест ни о чём");
        Assert.AreEqual(0, PartRegistryInstance.TakeGetAllCalls(),
            "обходы, случившиеся до включения замера, перешли в первый измеренный кадр: "
            + "имена у них уже забыты, и кадр получает чужое число без единого виновника");
    }

    [Test]
    public void RightAfterTheWindowStarts_BothHalvesAgree()
    {
        AMonitorThatIsNotMeasuringYet();
        TheSceneIsWalked(15);
        _monitor!.SimulateF9ForTests();

        TheSceneIsWalked(3);

        Assert.AreEqual(3, SceneScanLog.TimesNoted,
            "поимённая половина обязана видеть ровно три обхода");
        Assert.AreEqual(3, PartRegistryInstance.TakeGetAllCalls(),
            "и счётная половина — те же три: расхождение половин и печатается "
            + "как «(без имени: N)»");
    }

    [Test]
    public void AWalkAfterTheStart_IsStillCountedByBothHalves()
    {
        AMonitorThatIsNotMeasuringYet();
        _monitor!.SimulateF9ForTests();

        TheSceneIsWalked(1);

        Assert.AreEqual(1, SceneScanLog.TimesNoted,
            "слив на старте окна не имеет права глушить учёт вообще — тогда прибор "
            + "станет тихим, а тихий прибор хуже неточного");
        Assert.AreEqual(1, PartRegistryInstance.TakeGetAllCalls());
    }
}
