using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Время жизни скрытого поля-гейта, которым строка привязки перехватывает клавишу.
///
/// Первая версия строила это поле один раз, при сборке окна, и оставляла в сцене
/// навсегда. Стоило это 53 красных PlayMode-снимка (поле попало в кадры сцен, где
/// настройки даже не открывались), но снимки тут — симптом. Дефект в другом:
/// <c>CameraController.IsTypingInInputField</c> — ГЛОБАЛЬНЫЙ гейт горячих клавиш,
/// и он спрашивает ровно одно — «на выбранном объекте есть TMP_InputField». Вечно
/// живущее невидимое поле ввода означает, что один случайный фокус на нём молча
/// отнимает у пользователя всю клавиатуру сцены, и повторяется это «иногда» —
/// худший класс бага.
///
/// Поэтому гейт существует ровно столько, сколько идёт захват, и проверяется это
/// с обоих концов: пока захват идёт — поле есть и горячие клавиши погашены; как
/// только он кончился любым путём (клавиша, Escape, закрытие окна), в сцене не
/// остаётся ни одного следа.
/// </summary>
public class KeybindingCaptureGateTests
{
    private GameObject? _canvasGo;
    private KeybindingCaptureGate? _gate;

    [SetUp]
    public void SetUp()
    {
        UIFactory.EnsureEventSystem();
        _canvasGo = new GameObject("CaptureGateCanvas");
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGo.AddComponent<CanvasScaler>();
        _canvasGo.AddComponent<GraphicRaycaster>();

        var gate = _canvasGo.AddComponent<KeybindingCaptureGate>();
        gate.Build(_canvasGo.transform);
        _gate = gate;
    }

    [TearDown]
    public void TearDown()
    {
        _gate?.CancelIfCapturing();
        if (_canvasGo != null) UnityEngine.Object.DestroyImmediate(_canvasGo!);
        var events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null) events.SetSelectedGameObject(null);
        CommandStack.Clear();
    }

    [Test]
    public void GateJustBuilt_PutsNothingInTheScene()
    {
        Assert.IsFalse(_gate!.IsCapturing, "собранный гейт не захватывает ничего сам по себе");
        CollectionAssert.IsEmpty(GuardNamesInTheScene(),
            "пока захвата нет, скрытого поля ввода в сцене быть не должно: именно оно "
            + "попало в 53 снимка сцен, где окно настроек не открывали");
    }

    [Test]
    public void WhileCapturing_TheGuardExists_AndSceneHotkeysAreGated()
    {
        _gate!.Begin(_ => { });

        Assert.IsTrue(_gate!.IsCapturing);
        Assert.AreEqual(1, GuardNamesInTheScene().Count,
            "на время захвата поле есть — без этого проверки «после захвата пусто» "
            + "зеленели бы, даже если бы поле не создавалось никогда");
        Assert.IsTrue(CameraController.IsTypingInInputField(),
            "ради этого гейт и существует: пока ждём клавишу, WASD/F1/зум сцены молчат");
    }

    [Test]
    public void AfterEscape_NothingIsLeftInTheScene_AndHotkeysComeBack()
    {
        var results = new List<KeyChordCapture.Result>();
        _gate!.Begin(results.Add);

        bool swallowed = _gate!.Read(KeyCode.Escape, ctrl: false, alt: false, shift: false);

        Assert.IsTrue(swallowed, "Escape во время захвата не уходит дальше в приложение");
        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].WasCancelled, "Escape отменяет захват, а не назначает клавишу");
        Assert.IsFalse(_gate!.IsCapturing);
        CollectionAssert.IsEmpty(GuardNamesInTheScene(), "после отмены в сцене пусто");
        Assert.IsFalse(CameraController.IsTypingInInputField(),
            "горячие клавиши сцены обязаны ожить сразу после отмены");
    }

    [Test]
    public void AfterAChordIsCaptured_NothingIsLeftInTheScene()
    {
        var results = new List<KeyChordCapture.Result>();
        _gate!.Begin(results.Add);

        bool swallowed = _gate!.Read(KeyCode.K, ctrl: true, alt: false, shift: true);

        Assert.IsTrue(swallowed, "записанная клавиша не уходит дальше в приложение");
        Assert.AreEqual(1, results.Count);
        Assert.IsFalse(results[0].WasCancelled);
        Assert.AreEqual(new KeyChord(KeyCode.K, ctrl: true, shift: true), results[0].Chord);
        Assert.IsFalse(_gate!.IsCapturing);
        CollectionAssert.IsEmpty(GuardNamesInTheScene(), "после успешного назначения в сцене пусто");
        Assert.IsFalse(CameraController.IsTypingInInputField());
    }

    /// <summary>Голый модификатор захват не заканчивает — значит и поле остаётся на
    /// месте: «пусто в сцене» обязано наступать ровно тогда, когда захват КОНЧИЛСЯ,
    /// а не при первом же нажатии чего угодно.</summary>
    [Test]
    public void ABareModifier_KeepsTheCaptureAndItsGuardAlive()
    {
        var results = new List<KeyChordCapture.Result>();
        _gate!.Begin(results.Add);

        bool swallowed = _gate!.Read(KeyCode.LeftControl, ctrl: true, alt: false, shift: false);

        Assert.IsTrue(swallowed, "нажатие модификатора тоже не уходит в приложение");
        CollectionAssert.IsEmpty(results, "один Ctrl — ещё не привязка");
        Assert.IsTrue(_gate!.IsCapturing);
        Assert.AreEqual(1, GuardNamesInTheScene().Count);
    }

    [Test]
    public void SecondBegin_ReplacesTheFirstCapture_WithoutLeavingASecondGuard()
    {
        var first = new List<KeyChordCapture.Result>();
        _gate!.Begin(first.Add);
        _gate!.Begin(_ => { });

        Assert.AreEqual(1, first.Count, "первый захват обязан завершиться");
        Assert.IsTrue(first[0].WasCancelled, "и именно отменой — клавишу ему никто не давал");
        Assert.AreEqual(1, GuardNamesInTheScene().Count,
            "второе поле поверх первого — это два невидимых инпута в сцене вместо нуля");
    }

    [Test]
    public void DestroyingTheOwner_MidCapture_LeavesNothingBehind()
    {
        _gate!.Begin(_ => { });
        Assume.That(GuardNamesInTheScene().Count, Is.EqualTo(1));

        UnityEngine.Object.DestroyImmediate(_canvasGo!);
        _canvasGo = null;
        _gate = null;

        CollectionAssert.IsEmpty(GuardNamesInTheScene(),
            "уничтожение владельца во время захвата не оставляет висящего поля ввода");
    }

    // ── настоящее окно настроек ─────────────────────────────

    [Test]
    public void SettingsWindow_BuiltButNeverCaptured_HasNoGuardInTheScene()
    {
        var host = NewSettingsWindow(out var settings);
        try
        {
            CollectionAssert.IsEmpty(GuardNamesInTheScene(),
                "снимки 53 сцен ловили именно это: окно собрано, захвата не было, "
                + "а поле ввода уже в дереве UI");
            Assert.IsFalse(CameraController.IsTypingInInputField(),
                "и горячие клавиши сцены ничем не заняты");
            Assert.IsNotNull(settings, "окно обязано было собраться — иначе проверка пуста по ошибке");
        }
        finally
        {
            EditModeManager.Reset();
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void ClosingTheSettingsWindow_MidCapture_LeavesNoGuard()
    {
        var host = NewSettingsWindow(out var settings);
        try
        {
            settings!.OpenControlsTab();
            CellButton(host, "KbBtn_Undo_P").onClick.Invoke();
            Assume.That(GuardNamesInTheScene().Count, Is.EqualTo(1),
                "клик по ячейке обязан начать захват — иначе закрывать нечего");

            settings!.SetVisible(false);

            CollectionAssert.IsEmpty(GuardNamesInTheScene(),
                "закрытие окна прямо во время захвата не оставляет поле ввода в сцене");
            Assert.IsFalse(CameraController.IsTypingInInputField(),
                "и не оставляет горячие клавиши сцены погашенными");
        }
        finally
        {
            EditModeManager.Reset();
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static GameObject NewSettingsWindow(out SettingsPanelUI? settings)
    {
        var canvas = UIFactory.CreateCanvas("SettingsCanvasForCaptureGate");
        settings = canvas.gameObject.AddComponent<SettingsPanelUI>();
        settings!.Build(canvas.transform);
        return canvas.gameObject;
    }

    private static Button CellButton(GameObject host, string name)
    {
        var button = host.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
        Assert.IsNotNull(button, "кнопки ячейки привязки «" + name + "» в окне нет");
        return button!;
    }

    private static List<string> GuardNamesInTheScene()
    {
        var found = new List<string>();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            CollectGuards(root.transform, root.name, found);
        return found;
    }

    private static void CollectGuards(Transform node, string path, List<string> into)
    {
        if (node.name == KeybindingCaptureGate.GuardName) into.Add(path);
        for (int i = 0; i < node.childCount; i++)
        {
            var child = node.GetChild(i);
            CollectGuards(child, path + "/" + child.name, into);
        }
    }
}
