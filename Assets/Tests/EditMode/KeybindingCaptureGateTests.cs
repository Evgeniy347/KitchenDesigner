using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;

/// <summary>
/// Пока строка привязки ждёт клавишу, горячие клавиши сцены обязаны молчать - иначе
/// назначаемая клавиша заодно двигает камеру или открывает панель.
///
/// Первая версия держала это на невидимом <c>TMP_InputField</c>, который жил один раз, при
/// сборке окна, и оставался в сцене навсегда - 53 красных PlayMode-снимка (поле попало в
/// кадры сцен, где настройки даже не открывали).
///
/// Вторая версия (коммит 53344414) чинила время жизни поля, но глушение по-прежнему стояло
/// на фокусе `EventSystem`: `CameraController.IsTypingInInputField` спрашивает, есть ли
/// `TMP_InputField` на выбранном объекте. `EventSystem.current` - это первый элемент
/// статического списка, который соседние тестовые классы то поднимают, то сносят по ходу
/// прогона (`ContextMenuRefreshBugTests`, `FieldHighlightTests`, `SettingsPanelUITests` и
/// другие) - "система событий в сцене есть" была совпадением расписания, а не инвариантом.
/// Захват писал `EventSystem.current?.SetSelectedGameObject(...)` (C#-null), а гашение
/// спрашивало `es == null` (Unity-null) - на пустом списке или на уничтоженном элементе эти
/// два вопроса расходились, и полный прогон EditMode падал там, где одиночный зеленел.
///
/// Третья версия (эта) убирает фокус, `EventSystem` и скрытое поле целиком. Глушение - это
/// явное владение внутри `InputMap` (`MuteSceneInput`/`UnmuteSceneInput`/`IsMutedBy`):
/// включивший обязан выключить, посторонний не может снять чужое глушение, а если владелец
/// умирает, не выключив его сам, `InputMap` видит это через Unity-null-сравнение владельца
/// (`_muteOwner != null` на уничтоженном объекте - `false`) и глушение снимается само.
/// `IsCapturing` на гейте - не отдельный флаг, а прямое чтение того же владения
/// (`InputMap.IsMutedBy(this)`): одно состояние, а не два описания одного и того же.
/// </summary>
public class KeybindingCaptureGateTests
{
    private GameObject? _hostGo;
    private KeybindingCaptureGate? _gate;

    [SetUp]
    public void SetUp()
    {
        _hostGo = new GameObject("CaptureGateHost");
        var gate = _hostGo.AddComponent<KeybindingCaptureGate>();
        gate.Build(_hostGo.transform);
        _gate = gate;
    }

    [TearDown]
    public void TearDown()
    {
        _gate?.CancelIfCapturing();
        if (_hostGo != null) UnityEngine.Object.DestroyImmediate(_hostGo);
        InputMap.ReleaseAnyMuteForTests();
        CommandStack.Clear();
    }

    [Test]
    public void GateJustBuilt_IsNotCapturing_AndDoesNotMuteTheScene()
    {
        Assert.IsFalse(_gate!.IsCapturing, "собранный гейт не захватывает ничего сам по себе");
        Assert.IsFalse(InputMap.SceneInputMuted, "и не глушит горячие клавиши до первого Begin");
    }

    [Test]
    public void WhileCapturing_SceneHotkeysAreMuted()
    {
        _gate!.Begin(_ => { });

        Assert.IsTrue(_gate!.IsCapturing);
        Assert.IsTrue(InputMap.IsMutedBy(_gate!),
            "гейт обязан быть владельцем глушения - а не просто одним из тех, кто его включил");
        Assert.IsFalse(
            InputMap.DownWithSimulatedKeyForTests(InputAction.CameraMoveForward, KeyCode.W),
            "ради этого гейт и существует: пока ждём клавишу, WASD/F1/зум сцены молчат");
    }

    [Test]
    public void AfterEscape_MuteIsLifted_AndHotkeysComeBack()
    {
        var results = new List<KeyChordCapture.Result>();
        _gate!.Begin(results.Add);

        bool swallowed = _gate!.Read(KeyCode.Escape, ctrl: false, alt: false, shift: false);

        Assert.IsTrue(swallowed, "Escape во время захвата не уходит дальше в приложение");
        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].WasCancelled, "Escape отменяет захват, а не назначает клавишу");
        Assert.IsFalse(_gate!.IsCapturing);
        Assert.IsFalse(InputMap.SceneInputMuted, "после отмены горячие клавиши сцены обязаны ожить сразу");
        Assert.IsTrue(InputMap.DownWithSimulatedKeyForTests(InputAction.CameraMoveForward, KeyCode.W));
    }

    [Test]
    public void AfterAChordIsCaptured_MuteIsLifted()
    {
        var results = new List<KeyChordCapture.Result>();
        _gate!.Begin(results.Add);

        bool swallowed = _gate!.Read(KeyCode.K, ctrl: true, alt: false, shift: true);

        Assert.IsTrue(swallowed, "записанная клавиша не уходит дальше в приложение");
        Assert.AreEqual(1, results.Count);
        Assert.IsFalse(results[0].WasCancelled);
        Assert.AreEqual(new KeyChord(KeyCode.K, ctrl: true, shift: true), results[0].Chord);
        Assert.IsFalse(_gate!.IsCapturing);
        Assert.IsFalse(InputMap.SceneInputMuted, "после успешного назначения глушение обязано сняться");
    }

    /// <summary>Голый модификатор захват не заканчивает - значит и глушение остаётся: "молчание
    /// снято" обязано наступать ровно тогда, когда захват КОНЧИЛСЯ, а не при первом же
    /// нажатии чего угодно.</summary>
    [Test]
    public void ABareModifier_KeepsTheCaptureAndTheMuteAlive()
    {
        var results = new List<KeyChordCapture.Result>();
        _gate!.Begin(results.Add);

        bool swallowed = _gate!.Read(KeyCode.LeftControl, ctrl: true, alt: false, shift: false);

        Assert.IsTrue(swallowed, "нажатие модификатора тоже не уходит в приложение");
        CollectionAssert.IsEmpty(results, "один Ctrl - ещё не привязка");
        Assert.IsTrue(_gate!.IsCapturing);
        Assert.IsTrue(InputMap.SceneInputMuted);
    }

    [Test]
    public void SecondBegin_ReplacesTheFirstCapture_WithoutLosingMuteOwnership()
    {
        var first = new List<KeyChordCapture.Result>();
        _gate!.Begin(first.Add);
        _gate!.Begin(_ => { });

        Assert.AreEqual(1, first.Count, "первый захват обязан завершиться");
        Assert.IsTrue(first[0].WasCancelled, "и именно отменой - клавишу ему никто не давал");
        Assert.IsTrue(_gate!.IsCapturing, "второй захват тем же гейтом обязан идти дальше");
        Assert.IsTrue(InputMap.IsMutedBy(_gate!),
            "владение глушением не должно потеряться между первым и вторым Begin");
    }

    [Test]
    public void DestroyingTheOwner_MidCapture_LiftsTheMute()
    {
        _gate!.Begin(_ => { });
        Assume.That(InputMap.SceneInputMuted, Is.True);

        UnityEngine.Object.DestroyImmediate(_hostGo!);
        _hostGo = null;
        _gate = null;

        Assert.IsFalse(InputMap.SceneInputMuted,
            "уничтожение владельца во время захвата не должно оставлять сцену немой");
    }

    // ── настоящее окно настроек ─────────────────────────────

    [Test]
    public void SettingsWindow_BuiltButNeverCaptured_DoesNotMuteTheScene()
    {
        var host = NewSettingsWindow(out var settings);
        try
        {
            Assert.IsFalse(InputMap.SceneInputMuted,
                "окно собрано, захвата не было - горячие клавиши сцены ничем не заняты");
            Assert.IsNotNull(settings, "окно обязано было собраться - иначе проверка пуста по ошибке");
        }
        finally
        {
            EditModeManager.Reset();
            UnityEngine.Object.DestroyImmediate(host);
            InputMap.ReleaseAnyMuteForTests();
        }
    }

    [Test]
    public void ClosingTheSettingsWindow_MidCapture_LiftsTheMute()
    {
        var host = NewSettingsWindow(out var settings);
        try
        {
            settings!.OpenControlsTab();
            CellButton(host, "KbBtn_Undo_P").onClick.Invoke();
            Assume.That(InputMap.SceneInputMuted, Is.True,
                "клик по ячейке обязан начать захват - иначе закрывать нечего");

            settings!.SetVisible(false);

            Assert.IsFalse(InputMap.SceneInputMuted,
                "закрытие окна прямо во время захвата не должно оставлять горячие клавиши сцены немыми");
        }
        finally
        {
            EditModeManager.Reset();
            UnityEngine.Object.DestroyImmediate(host);
            InputMap.ReleaseAnyMuteForTests();
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
}
