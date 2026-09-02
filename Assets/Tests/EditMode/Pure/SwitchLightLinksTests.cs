using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Pure
{
    /// <summary>Связь «выключатель → светильники» хранится ТОЛЬКО у выключателя,
    /// списком имён — ровно так же, как KitchenElement.AttachedToName хранит
    /// привязку к родителю. Обратной стороны (какие выключатели у светильника)
    /// в данных нет: она каждый раз вычисляется перебором выключателей. Это и
    /// есть причина, по которой связь получается многие-ко-многим бесплатно и
    /// не может рассинхронизироваться — дублировать нечего.
    ///
    /// Правило горения: светильник БЕЗ единой связи ведёт себя как раньше и
    /// слушается общего выключателя света (GlobalOn). Как только его назвал
    /// хотя бы один выключатель, решают выключатели — горит, если включён хотя
    /// бы один из них. GlobalOn остаётся рубильником: выключенный свет гасит
    /// всё, иначе «выключить свет» перестало бы работать в комнате, где стоит
    /// хоть один включённый выключатель.
    ///
    /// Имя — ключ, а имена меняются. Удалённый светильник просто перестаёт
    /// находиться (Sanitized выкидывает имя, которого нет в сцене), а
    /// переименованный обязан быть переписан в списках ВСЕХ выключателей сразу
    /// (Renamed), иначе связь тихо порвётся — тот же класс ошибки, что
    /// DrawerLinks.Rename чинит для AttachedToName.</summary>
    public class SwitchLightLinksTests
    {
        private static SwitchLinkState Switch(string name, bool on, params string[] lights)
            => new SwitchLinkState(name, on, lights);

        private static List<SwitchLinkState> Scene(params SwitchLinkState[] switches)
            => new List<SwitchLinkState>(switches);

        [Test]
        public void ALightNobodyNamed_StillObeysTheGlobalLightSwitch()
        {
            var scene = Scene(Switch("S1", false, "Люстра"));

            Assert.IsTrue(SwitchLightLinks.IsLit("Бра", scene, true),
                "Светильник без связей обязан вести себя как раньше — гореть при общем свете");
            Assert.IsFalse(SwitchLightLinks.IsLit("Бра", scene, false),
                "И гаснуть вместе с общим светом");
        }

        [Test]
        public void OneSwitch_LightsEveryLampItNames()
        {
            var scene = Scene(Switch("S1", true, "Люстра", "Бра", "Подсветка"));

            foreach (var lamp in new[] { "Люстра", "Бра", "Подсветка" })
                Assert.IsTrue(SwitchLightLinks.IsLit(lamp, scene, true),
                    $"Один выключатель управляет несколькими светильниками: {lamp} обязан гореть");
        }

        [Test]
        public void SeveralSwitchesOnOneLamp_LightItIfAnySingleOneIsOn()
        {
            var lampOnThreeSwitches = Scene(
                Switch("Вход", false, "Люстра"),
                Switch("Кровать", true, "Люстра"),
                Switch("Коридор", false, "Люстра"));

            Assert.IsTrue(SwitchLightLinks.IsLit("Люстра", lampOnThreeSwitches, true),
                "Проходной выключатель: одного включённого достаточно, чтобы светильник горел");
            Assert.AreEqual(3,
                SwitchLightLinks.ControllingSwitchNames("Люстра", lampOnThreeSwitches).Count,
                "Обратная сторона связи считается перебором и обязана найти все три");
        }

        [Test]
        public void SeveralSwitchesOnOneLamp_LeaveItDarkOnlyWhenEveryOneIsOff()
        {
            var scene = Scene(
                Switch("Вход", false, "Люстра"),
                Switch("Кровать", false, "Люстра"));

            Assert.IsFalse(SwitchLightLinks.IsLit("Люстра", scene, true),
                "Пока выключены все, светильник не горит — иначе выключатель ничего не решает");
        }

        [Test]
        public void TheGlobalSwitch_OutranksEveryLocalSwitch()
        {
            var scene = Scene(Switch("Вход", true, "Люстра"));

            Assert.IsFalse(SwitchLightLinks.IsLit("Люстра", scene, false),
                "«Выключить свет» обязано гасить и то, что включено локальным выключателем");
        }

        [Test]
        public void ASwitchNamingALampThatIsGone_ControlsNothing()
        {
            var scene = Scene(Switch("Вход", true, "Удалённая люстра"));

            Assert.IsFalse(SwitchLightLinks.IsControlled("Бра", scene),
                "Мёртвая ссылка не имеет права заодно захватить чужой светильник");
            Assert.IsTrue(SwitchLightLinks.IsLit("Бра", scene, true),
                "И не имеет права влиять на светильники, которых она не называет");
        }

        [Test]
        public void ADeletedLamp_FallsOutOfTheListTheNextTimeItIsSanitized()
        {
            var live = new List<string> { "Бра" };
            var kept = SwitchLightLinks.Sanitized(new[] { "Люстра", "Бра" }, live);

            Assert.AreEqual(1, kept.Count,
                "Имени удалённого светильника нечего делать в списке — иначе счётчик секции врёт");
            Assert.AreEqual("Бра", kept[0],
                "Выживает ровно тот светильник, который остался в сцене");
        }

        [Test]
        public void SanitizedWithoutASceneList_KeepsEveryNameButDropsDuplicatesAndBlanks()
        {
            var kept = SwitchLightLinks.Sanitized(new[] { "Бра", "", "Бра", "Люстра" }, null);

            Assert.AreEqual(new[] { "Бра", "Люстра" }, kept,
                "Два раза один светильник — это одна связь, а пустое имя не связь вообще");
        }

        [Test]
        public void Sanitized_NeverExceedsTheCeiling()
        {
            var many = new List<string>();
            for (int i = 0; i < SwitchLightLinks.MaxLightsPerSwitch * 2; i++) many.Add("L" + i);

            Assert.AreEqual(SwitchLightLinks.MaxLightsPerSwitch,
                SwitchLightLinks.Sanitized(many, null).Count,
                "Потолок связей на выключатель обязан держаться: строк в секции ровно столько");
        }

        [Test]
        public void RenamingALamp_RewritesItInTheSwitchListInsteadOfBreakingTheLink()
        {
            var after = SwitchLightLinks.Renamed(new[] { "Люстра", "Бра" }, "Люстра", "Люстра 2");

            Assert.AreEqual(new[] { "Люстра 2", "Бра" }, after,
                "Ссылка живёт по имени: не переписал — связь порвалась молча");
        }

        [Test]
        public void RenamingALampOntoANameTheSwitchAlreadyHas_MergesTheTwoLinks()
        {
            var after = SwitchLightLinks.Renamed(new[] { "Люстра", "Бра" }, "Люстра", "Бра");

            Assert.AreEqual(new[] { "Бра" }, after,
                "Два одинаковых имени в одном списке — это одна связь, а не две строки-близнеца");
        }

        [Test]
        public void RenamingSomethingTheSwitchNeverNamed_ChangesNothing()
        {
            var after = SwitchLightLinks.Renamed(new[] { "Люстра" }, "Стол", "Стол 2");

            Assert.AreEqual(new[] { "Люстра" }, after,
                "Переименование постороннего элемента не имеет права трогать связи");
        }

        [Test]
        public void AddingALampTwice_LeavesOneLink()
        {
            var once = SwitchLightLinks.WithAdded(new[] { "Бра" }, "Люстра");
            var twice = SwitchLightLinks.WithAdded(once, "Люстра");

            Assert.AreEqual(2, twice.Count,
                "Повторное добавление того же светильника не создаёт вторую строку");
        }

        [Test]
        public void AddingBeyondTheCeiling_IsRefusedRatherThanTruncatingSomethingElse()
        {
            var full = new List<string>();
            for (int i = 0; i < SwitchLightLinks.MaxLightsPerSwitch; i++) full.Add("L" + i);

            var after = SwitchLightLinks.WithAdded(full, "Ещё один");

            Assert.AreEqual(full, after,
                "На потолке добавление ничего не меняет — старые связи не жертвуются новой");
        }

        [Test]
        public void RemovingByRow_TakesExactlyThatRow()
        {
            var after = SwitchLightLinks.WithRemovedAt(new[] { "A", "B", "C" }, 1);

            Assert.AreEqual(new[] { "A", "C" }, after,
                "Кнопка удаления в строке обязана убрать светильник этой строки");
        }

        [Test]
        public void RemovingARowThatDoesNotExist_LeavesTheListAlone()
        {
            var after = SwitchLightLinks.WithRemovedAt(new[] { "A" }, 5);

            Assert.AreEqual(new[] { "A" }, after,
                "Индекс мимо списка — это рассинхрон UI, а не команда всё стереть");
        }

        [Test]
        public void ReplacingARowWithALampTheSwitchAlreadyHas_IsRefused()
        {
            var after = SwitchLightLinks.WithReplacedAt(new[] { "A", "B" }, 0, "B");

            Assert.AreEqual(new[] { "A", "B" }, after,
                "Выпадающий список не имеет права свести две строки к одному светильнику: "
                + "одна строка молча исчезла бы");
        }

        [Test]
        public void ReplacingARow_SwapsTheLampInPlace()
        {
            var after = SwitchLightLinks.WithReplacedAt(new[] { "A", "B" }, 1, "C");

            Assert.AreEqual(new[] { "A", "C" }, after,
                "Смена светильника в строке не переставляет остальные строки");
        }

        [Test]
        public void ControllingSwitchNames_ReportsEachSwitchOnce()
        {
            var scene = Scene(
                Switch("Вход", true, "Люстра", "Бра"),
                Switch("Кровать", false, "Люстра"));

            Assert.AreEqual(new[] { "Вход", "Кровать" },
                SwitchLightLinks.ControllingSwitchNames("Люстра", scene),
                "Список выключателей светильника вычисляется, а не хранится — и не дублируется");
            Assert.AreEqual(new[] { "Вход" },
                SwitchLightLinks.ControllingSwitchNames("Бра", scene),
                "Второй светильник видит только свой выключатель");
        }

        [Test]
        public void AnEmptyScene_LeavesEveryLampOnTheGlobalSwitch()
        {
            var empty = Scene();

            Assert.IsTrue(SwitchLightLinks.IsLit("Люстра", empty, true),
                "Пока выключателей нет, поведение обязано совпадать со старым");
            Assert.IsFalse(SwitchLightLinks.IsControlled("Люстра", null),
                "Отсутствие списка выключателей — это ноль связей, а не исключение");
        }
    }
}
