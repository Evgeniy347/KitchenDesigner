using System;
using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Демо-проект открывается ровно один раз — при первом запуске после
    /// установки — и до первого «сохранить копию» защищён от любых правок.
    ///
    /// Вся ловушка тут в путях. Демо лежит в папке УСТАНОВКИ, куда писать нельзя
    /// (а при per-user установке — можно, что ещё хуже: автосохранение молча
    /// перетёрло бы образец для всех следующих запусков). Поэтому режим снимает
    /// только сохранение в ДРУГОЙ файл, а сравнение путей обязано пережить и
    /// разный регистр, и прямые слэши: диалог Windows вернёт `C:\Users\...`,
    /// а собранный нами путь — `C:/Users/...`, и наивное `==` сочло бы копию
    /// «другим файлом» либо, наоборот, оставило бы режим включённым навсегда.
    ///
    /// Тесты нарочно проверяют не «вызвался ли метод», а СОСТОЯНИЕ после него:
    /// можно ли теперь менять сцену и можно ли автосохраняться.</summary>
    public class DemoModeTests
    {
        private const string Demo = @"C:\Program Files\Kitchen Designer\Demo\demo.json";
        private const string Mine = @"C:\Users\ivan\Documents\кухня.json";

        private DemoMode _mode = new DemoMode();

        [SetUp]
        public void SetUp() => _mode = new DemoMode();

        [Test]
        public void FreshState_AllowsEverything()
        {
            Assert.IsFalse(_mode.IsActive, "без демо-проекта программа работает как обычно");
            Assert.IsTrue(_mode.MutationsAllowed);
            Assert.IsTrue(_mode.AutoSaveAllowed);
        }

        [Test]
        public void Enter_ForbidsMutationsAndAutoSave()
        {
            _mode.Enter(Demo);

            Assert.IsTrue(_mode.IsActive);
            Assert.IsFalse(_mode.MutationsAllowed,
                "правка демо-проекта обязана быть отклонена, а не применена молча");
            Assert.IsFalse(_mode.AutoSaveAllowed,
                "автосохранение пишет в открытый проект — то есть в файл установки; "
                + "при per-user установке папка ДОСТУПНА на запись, и образец был бы "
                + "перетёрт первой же минутой простоя");
        }

        [Test]
        public void Enter_RefusesAnEmptyPath()
        {
            Assert.Throws<ArgumentException>(() => _mode.Enter(""),
                "режим без пути к демо-файлу нечем снять: сравнивать сохранение будет не с чем, "
                + "и программа осталась бы навсегда только для чтения");
        }

        [Test]
        public void SavingToAnotherFile_LiftsTheMode()
        {
            _mode.Enter(Demo);

            _mode.ProjectSavedTo(Mine);

            Assert.IsFalse(_mode.IsActive, "копия сохранена — дальше это обычный проект пользователя");
            Assert.IsTrue(_mode.MutationsAllowed);
            Assert.IsTrue(_mode.AutoSaveAllowed);
            Assert.AreEqual(string.Empty, _mode.DemoPath,
                "путь к образцу больше ничего не защищает и не должен переживать выход из режима");
        }

        [Test]
        public void SavingBackOntoTheDemoFile_KeepsTheMode()
        {
            _mode.Enter(Demo);

            _mode.ProjectSavedTo(Demo);

            Assert.IsTrue(_mode.IsActive,
                "запись в сам образец — это не «сохранил к себе»; сняв режим здесь, "
                + "мы бы разрешили автосохранению дописывать папку установки");
        }

        [Test]
        public void SavingToTheDemoFileWrittenDifferently_KeepsTheMode()
        {
            _mode.Enter(Demo);

            _mode.ProjectSavedTo("c:/program files/kitchen designer/demo/DEMO.json");

            Assert.IsTrue(_mode.IsActive,
                "Windows не различает регистр и оба слэша: тот же файл, записанный иначе, "
                + "обязан остаться тем же файлом");
        }

        [Test]
        public void CancelledSaveDialog_LeavesTheModeOn()
        {
            _mode.Enter(Demo);

            _mode.ProjectSavedTo("");

            Assert.IsTrue(_mode.IsActive,
                "пустой путь — это отменённый диалог: ничего не сохранено, защита остаётся");
        }

        [Test]
        public void LoadingAnotherProject_LiftsTheMode()
        {
            _mode.Enter(Demo);

            _mode.ProjectLoadedFrom(Mine);

            Assert.IsFalse(_mode.IsActive,
                "демо на экране больше нет — защищать нечего, иначе свой же открытый "
                + "проект остался бы нередактируемым");
        }

        [Test]
        public void IsDemoFile_AnswersOnlyWhileTheModeIsOn()
        {
            Assert.IsFalse(_mode.IsDemoFile(Demo),
                "вне демо-режима тот же путь — обычный файл, запрещать запись в него не за что");

            _mode.Enter(Demo);

            Assert.IsTrue(_mode.IsDemoFile(Demo));
            Assert.IsFalse(_mode.IsDemoFile(Mine));
        }

        [Test]
        public void ShouldOpenDemo_OnlyOnTheVeryFirstRun()
        {
            Assert.IsTrue(DemoMode.ShouldOpenDemo(firstRunRecorded: false, hasOwnProject: false, demoFileExists: true),
                "первый запуск после установки — единственный случай, когда демо показывают");
        }

        [Test]
        public void ShouldOpenDemo_NeverAgainAfterTheFirstRun()
        {
            Assert.IsFalse(DemoMode.ShouldOpenDemo(firstRunRecorded: true, hasOwnProject: false, demoFileExists: true),
                "демо, всплывающее каждый запуск, — это не витрина, а помеха");
        }

        [Test]
        public void ShouldOpenDemo_NeverOverAnExistingProject()
        {
            Assert.IsFalse(DemoMode.ShouldOpenDemo(firstRunRecorded: false, hasOwnProject: true, demoFileExists: true),
                "обновление поверх старой версии выглядит как «первый запуск» — отметки о запуске "
                + "в PlayerPrefs ещё нет. У такого пользователя уже есть свой проект, и подменять "
                + "его демо-режимом только для чтения было бы порчей рабочего дня");
        }

        [Test]
        public void ShouldOpenDemo_NeverWithoutTheFile()
        {
            Assert.IsFalse(DemoMode.ShouldOpenDemo(firstRunRecorded: false, hasOwnProject: false, demoFileExists: false),
                "сборка из исходников и портативная распаковка идут без папки Demo — "
                + "пустой экран лучше, чем ошибка чтения");
        }

        [Test]
        public void ResetCurrent_GivesAFreshSingleton()
        {
            DemoMode.Current.Enter(Demo);

            DemoMode.ResetCurrent();

            Assert.IsFalse(DemoMode.Current.IsActive,
                "глобальный экземпляр переживает тест — без сброса один упавший тест "
                + "запер бы всю остальную сюиту в режиме только для чтения");
        }
    }
}
