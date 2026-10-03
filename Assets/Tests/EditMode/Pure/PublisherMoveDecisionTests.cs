using System;
using System.Collections.Generic;
using KitchenDesigner.Core;
using NUnit.Framework;

// Смена companyName (DefaultCompany → Evgeniy347) переселяет PlayerPrefs и persistentDataPath.
// Перенос делается один раз и только когда старое место хранит данные, а новое — нет:
// при данных в обоих местах никто не может решить за пользователя, какие главнее.
public class PublisherMoveDecisionTests
{
    [TestCase(false, false, PublisherMoveStep.NothingToMove)]
    [TestCase(false, true, PublisherMoveStep.NothingToMove)]
    [TestCase(true, false, PublisherMoveStep.Move)]
    [TestCase(true, true, PublisherMoveStep.BothHoldData)]
    public void Decide_MovesOnlyFromAFullOldStoreIntoAnEmptyNewOne(bool oldHoldsData, bool newHoldsData, PublisherMoveStep expected)
    {
        Assert.AreEqual(expected, PublisherMoveDecision.Decide(oldHoldsData, newHoldsData),
            $"старое={(oldHoldsData ? "есть" : "нет")}, новое={(newHoldsData ? "есть" : "нет")}");
    }

    // Имена сняты с реального ключа HKCU\Software\DefaultCompany\KitchenDesigner2 (2026-10-03).
    // Движок пишет свои значения в НОВЫЙ ключ ещё до первого скрипта (размер окна, сессия),
    // поэтому «новое место пусто» означает «в нём только записи движка» — иначе перенос
    // не случился бы никогда.
    [TestCase("Screenmanager Resolution Width_h182942802", true)]
    [TestCase("Screenmanager Fullscreen mode Default_h401710285", true)]
    [TestCase("UnitySelectMonitor_h17969598", true)]
    [TestCase("unity.player_session_count_h922449978", true)]
    [TestCase("unity_connect.installation_id_h932087899", true)]
    [TestCase("KitchenSettings_h3206251286", false)]
    [TestCase("KitchenFirstRunDone_h3441148672", false)]
    [TestCase("KitchenSidebarPreset_Р¤РёС‚РёРЅРі_h2305417706", false)]
    [TestCase("Language_h3872303031", false)]
    public void EnginePreferences_AreTheOnlyOnesThatDoNotCountAsUserData(string valueName, bool engineOwned)
    {
        Assert.AreEqual(engineOwned, EngineOwnedEntries.IsEnginePreference(valueName), valueName);
    }

    [TestCase("Player.log", true)]
    [TestCase("Player-prev.log", true)]
    [TestCase("saves", false)]
    [TestCase("autosave.json", false)]
    [TestCase("perf.csv", false)]
    public void EngineFiles_AreTheOnlyOnesThatDoNotCountAsUserData(string entryName, bool engineOwned)
    {
        Assert.AreEqual(engineOwned, EngineOwnedEntries.IsEngineFile(entryName), entryName);
    }

    [Test]
    public void Run_BothHoldData_WarnsAndNeverMoves()
    {
        var store = new FakeStore(oldHoldsData: true, newHoldsData: true);
        var log = new RecordingLog();

        Assert.AreEqual(PublisherMoveOutcome.LeftBothAlone, PublisherMigration.Run(store, log.Log));
        Assert.AreEqual(0, store.Moves, "при данных в обоих местах перенос не вызывается вовсе");
        Assert.AreEqual(1, log.Warnings.Count, "пользователь должен узнать, почему его данные не переехали");
    }

    [Test]
    public void Run_OldFullNewEmpty_MovesOnceAndSaysSo()
    {
        var store = new FakeStore(oldHoldsData: true, newHoldsData: false);
        var log = new RecordingLog();

        Assert.AreEqual(PublisherMoveOutcome.Moved, PublisherMigration.Run(store, log.Log));
        Assert.AreEqual(1, store.Moves);
        Assert.AreEqual(1, log.Infos.Count);
        Assert.IsEmpty(log.Warnings);
    }

    [Test]
    public void Run_NothingOld_IsSilent()
    {
        var store = new FakeStore(oldHoldsData: false, newHoldsData: true);
        var log = new RecordingLog();

        Assert.AreEqual(PublisherMoveOutcome.NothingToMove, PublisherMigration.Run(store, log.Log));
        Assert.AreEqual(0, store.Moves);
        Assert.IsEmpty(log.Infos, "обычный запуск после переноса не должен ничего писать в лог");
        Assert.IsEmpty(log.Warnings);
    }

    [Test]
    public void Run_MoveThrows_ReportsAnErrorInsteadOfCrashingTheStartup()
    {
        var store = new FakeStore(oldHoldsData: true, newHoldsData: false) { Failure = new System.IO.IOException("занято") };
        var log = new RecordingLog();

        Assert.AreEqual(PublisherMoveOutcome.Failed, PublisherMigration.Run(store, log.Log),
            "исключение из хука SubsystemRegistration уронило бы запуск приложения");
        Assert.AreEqual(1, log.Errors.Count);
        StringAssert.Contains("занято", log.Errors[0], "причина должна дойти до Player.log");
    }

    private sealed class FakeStore : IPublisherStore
    {
        private readonly bool _old;
        private readonly bool _new;

        public FakeStore(bool oldHoldsData, bool newHoldsData)
        {
            _old = oldHoldsData;
            _new = newHoldsData;
        }

        public int Moves { get; private set; }
        public Exception? Failure { get; set; }
        public string Describe => "fake";
        public bool OldHoldsData() => _old;
        public bool NewHoldsData() => _new;

        public void Move()
        {
            Moves++;
            if (Failure != null) throw Failure;
        }
    }

    private sealed class RecordingLog
    {
        public readonly List<string> Infos = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public PublisherMigrationLog Log => new PublisherMigrationLog(Infos.Add, Warnings.Add, Errors.Add);
    }
}
