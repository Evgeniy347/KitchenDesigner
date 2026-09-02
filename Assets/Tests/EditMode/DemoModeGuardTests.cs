using System;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Демо-проект защищён В ОДНОЙ точке — <see cref="CommandStack.Execute"/>.
/// Это единственный шлюз правок сцены, и проверять защиту имеет смысл именно
/// здесь: закрыв кнопки в панелях, мы оставили бы открытыми MCP, горячие
/// клавиши и перетаскивание мышью.
///
/// Отклонённая правка не просто «не кладётся в стек» — она ОТКАТЫВАЕТСЯ.
/// Разница видна на создании и на перетаскивании: объект уже создан
/// (`CreateCommand` только включает его и регистрирует), элемент мышью уже
/// сдвинут, и команда приезжает в стек постфактум. Молча не выполнить её
/// значит оставить сцену изменённой и без шага отмены — ровно то состояние,
/// от которого демо-режим и защищает. Поэтому здесь считают `UndoCount`.</summary>
public class DemoModeGuardTests
{
    private const string DemoPath = @"C:\Program Files\Kitchen Designer\Demo\demo.json";
    private const string MyPath = @"C:\Users\ivan\кухня.json";

    private class FakeCommand : IUndoCommand
    {
        public int ExecCount;
        public int UndoCount;
        public string Description => "Fake";
        public void Execute() => ExecCount++;
        public void Undo() => UndoCount++;
    }

    private Action? _savedPrompt;
    private int _prompts;

    [SetUp]
    public void SetUp()
    {
        CommandStack.Clear();
        DemoMode.ResetCurrent();
        _savedPrompt = DemoModeGuard.Prompt;
        _prompts = 0;
        DemoModeGuard.Prompt = () => _prompts++;
    }

    [TearDown]
    public void TearDown()
    {
        DemoModeGuard.Prompt = _savedPrompt;
        DemoMode.ResetCurrent();
        CommandStack.Clear();
    }

    [Test]
    public void OutsideDemoMode_TheCommandRuns()
    {
        var command = new FakeCommand();

        CommandStack.Execute(command);

        Assert.AreEqual(1, command.ExecCount, "обычный проект правится как правился");
        Assert.AreEqual(0, _prompts, "без демо-режима объяснять пользователю нечего");
        Assert.IsTrue(CommandStack.CanUndo, "правка обязана попасть в историю отмены");
    }

    [Test]
    public void InDemoMode_TheCommandIsRolledBackInsteadOfApplied()
    {
        DemoMode.Current.Enter(DemoPath);
        var command = new FakeCommand();

        CommandStack.Execute(command);

        Assert.AreEqual(0, command.ExecCount, "правка демо-проекта не применяется");
        Assert.AreEqual(1, command.UndoCount,
            "мало не применить: перетаскивание и создание УЖЕ изменили сцену к моменту, "
            + "когда команда доезжает до стека — без отката демо осталось бы искажённым");
        Assert.IsFalse(CommandStack.CanUndo,
            "отклонённая правка не шаг истории: иначе Ctrl+Z «отменял» бы то, чего не было");
    }

    [Test]
    public void InDemoMode_TheUserIsToldWhyNothingHappened()
    {
        DemoMode.Current.Enter(DemoPath);

        CommandStack.Execute(new FakeCommand());

        Assert.AreEqual(1, _prompts,
            "молчаливый отказ читается как зависшая программа — окно объясняет, "
            + "что это образец и как получить свою копию");
    }

    [Test]
    public void InDemoMode_ACapturedBatchCommitsNothing()
    {
        DemoMode.Current.Enter(DemoPath);
        var command = new FakeCommand();

        CommandStack.BeginCapture();
        CommandStack.Execute(command);
        CommandStack.EndCapture("Правка панели", commit: true);

        Assert.AreEqual(0, command.ExecCount,
            "ContextMenuUI.Apply склеивает правки через BeginCapture/EndCapture — "
            + "этот путь обходит стек напрямую и обязан приезжать пустым");
        Assert.IsFalse(CommandStack.CanUndo);
    }

    [Test]
    public void AfterTheProjectIsSavedElsewhere_MutationsWorkAgain()
    {
        DemoMode.Current.Enter(DemoPath);
        DemoMode.Current.ProjectSavedTo(MyPath);
        var command = new FakeCommand();

        CommandStack.Execute(command);

        Assert.AreEqual(1, command.ExecCount,
            "копия сохранена — дальше это обычный проект, и запрет обязан сняться "
            + "без перезапуска программы");
        Assert.AreEqual(0, _prompts, "окно про демо больше не всплывает");
        Assert.IsTrue(CommandStack.CanUndo);
    }
}
