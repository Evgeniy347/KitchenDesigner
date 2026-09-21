using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Keybinding;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests.Geometry;

/// <summary>
/// У привязки, как и у аккорда, два представления: `InputBinding.Format` — это ХРАНЕНИЕ
/// (оно уходит в json и разбирается обратно), `InputBindingDisplay` — подпись на кнопке.
/// Вкладка по-русски, поэтому жест читается «ПКМ+движение», а в файле проекта лежит
/// «RMB+Move». Проверки те же три, что и у нумпада: хранение цело, подпись по-русски и
/// — сенсор — эти строки РАЗНЫЕ.
/// </summary>
public class InputBindingDisplayTests
{
    private static readonly MouseGesture[] EveryGestureShape =
    {
        new MouseGesture(MouseButtonKind.Left),
        new MouseGesture(MouseButtonKind.Right, withMotion: true),
        new MouseGesture(MouseButtonKind.Middle, withMotion: true),
        new MouseGesture(MouseButtonKind.XButton1),
        new MouseGesture(MouseButtonKind.XButton2),
        MouseGesture.Wheel(),
        new MouseGesture(MouseButtonKind.Left, ctrl: true),
    };

    [Test]
    public void Gestures_ReadInRussian()
    {
        Assert.AreEqual("ЛКМ", InputBindingDisplay.Of(
            InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Left))));
        Assert.AreEqual("ПКМ+движение", InputBindingDisplay.Of(
            InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Right, withMotion: true))));
        Assert.AreEqual("Колесо", InputBindingDisplay.Of(
            InputBinding.FromGesture(MouseGesture.Wheel())));
        Assert.AreEqual("Ctrl+ЛКМ", InputBindingDisplay.Of(
            InputBinding.FromGesture(new MouseGesture(MouseButtonKind.Left, ctrl: true))),
            "приставка модификаторов остаётся той же, что у хранения — второй список "
            + "«Ctrl+/Alt+/Shift+» разъехался бы с первым");
    }

    [Test]
    public void EveryGestureShape_HasARussianReading()
    {
        var unread = EveryGestureShape
            .Where(g => !InputBindingDisplay.TryReadingOf(g, out _))
            .Select(MouseGesture.Format)
            .ToList();

        CollectionAssert.IsEmpty(unread,
            "непрочитанная форма жеста всплывёт в панели английским токеном: "
            + string.Join(", ", unread));
    }

    [Test]
    public void Display_AndStorage_DifferForEveryGesture()
    {
        var same = EveryGestureShape
            .Where(g => InputBindingDisplay.Of(InputBinding.FromGesture(g)) == MouseGesture.Format(g))
            .Select(MouseGesture.Format)
            .ToList();

        CollectionAssert.IsEmpty(same,
            "сенсор: подпись и хранимая строка жеста обязаны различаться. Совпали — значит "
            + "либо подпись осталась английской, либо подменили формат хранения: "
            + string.Join(", ", same));
    }

    [Test]
    public void Storage_OfEveryGesture_StillRoundTrips()
    {
        foreach (var gesture in EveryGestureShape)
        {
            var binding = InputBinding.FromGesture(gesture);
            string stored = InputBinding.Format(binding);

            Assert.IsTrue(InputBinding.TryParse(stored, out var parsed), "не разобралось: " + stored);
            Assert.AreEqual(binding, parsed, "round-trip хранения поехал на " + stored);
            Assert.IsTrue(parsed.IsGesture, "жест обязан вернуться жестом, а не аккордом: " + stored);
        }
    }

    [Test]
    public void KeyBindings_AreStillShownByTheKeyFormatter()
    {
        var binding = InputBinding.FromKey(new KeyChord(KeyCode.KeypadPlus, ctrl: true));

        Assert.AreEqual(KeyChordDisplay.Of(new KeyChord(KeyCode.KeypadPlus, ctrl: true)),
            InputBindingDisplay.Of(binding),
            "клавиши показывает тот же форматтер, что и раньше — без второй таблицы");
    }

    [Test]
    public void EmptyBinding_ShowsNothing()
    {
        Assert.AreEqual(string.Empty, InputBindingDisplay.Of(InputBinding.Empty));
    }

    /// <summary>Тот же запрет, что и у клавишной подписи, и по той же причине: попади
    /// подпись в сохранение — в файл уедет «ПКМ+движение», чего `InputBinding.Parse`
    /// не разберёт, и привязка молча вернётся к заводской.</summary>
    [Test]
    public void TheDisplayFormatter_IsUsedByTheUiOnly_NeverBySaveOrLoad()
    {
        var core = RepoPaths.Subdir("Assets", "Scripts", "Core");
        var callers = new List<string>();

        foreach (var file in SourceCorpus.Files(core))
        {
            string name = Path.GetFileName(file);
            if (name == "InputBindingDisplay.cs") continue;
            if (SourceCorpus.Text(file).Contains("InputBindingDisplay")) callers.Add(name);
        }

        Assert.That(callers.Count, Is.GreaterThan(0),
            "скан не нашёл НИ ОДНОГО потребителя подписи — значит он смотрит не туда");
        CollectionAssert.DoesNotContain(callers, "KitchenSettings.Input.cs",
            "сохранение привязок обязано звать InputBinding.Format, а не подпись для кнопки");

        var offenders = callers
            .Where(f => f.Contains("Save") || f.Contains("Load") || f.Contains("Persist")
                || f.Contains("Json") || f.Contains("Data") || f.Contains("Restorer"))
            .ToList();
        CollectionAssert.IsEmpty(offenders,
            "подпись для кнопки попала в путь сохранения/загрузки: " + string.Join(", ", offenders));
    }

    [Test]
    public void EveryMouseActionDefault_HasAReadableCaption()
    {
        var mouseActions = InputActionCatalog.All
            .Where(a => InputActionCatalog.GroupOf(a) == InputActionGroup.Mouse)
            .ToList();

        Assert.That(mouseActions.Count, Is.GreaterThan(0),
            "в каталоге нет ни одного действия мыши — проверка ниже зеленела бы вхолостую");

        foreach (var action in mouseActions)
        {
            var binding = KeyBindingDefaults.PrimaryOf(action);
            Assert.IsTrue(binding.IsGesture, $"у действия мыши {action} дефолт обязан быть жестом");
            Assert.IsNotEmpty(InputBindingDisplay.Of(binding), $"пустая подпись у {action}");
        }
    }
}
