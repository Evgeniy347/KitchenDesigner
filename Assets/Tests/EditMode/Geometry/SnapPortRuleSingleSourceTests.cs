using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Устье порта описано в проекте ОДИН раз, и сторож не даёт появиться второму
    /// описанию.
    ///
    /// До этой работы про порты знал ровно один класс — `ScenePipeSnapshot`, и снэп про
    /// них не знал ничего: он оперировал парами ГРАНЕЙ коробок, а устье в этой геометрии
    /// не было представлено ничем. Из-за этого правильная позиция была неотличима от
    /// любой другой, и уголок не стыковался с трубой НИКОГДА (промах 11,73 мм). Лечится
    /// это не «добавить снэпу своё описание порта» — тогда описаний стало бы три, и они
    /// разошлись бы молча, как уже расходились правило отбора и `SnapSystem.Diagnose`
    /// (78 ложных находок, см. `SnapMountRuleSingleSourceTests`).
    ///
    /// Поэтому источник ровно один и он ВЕРТИКАЛЬНЫЙ:
    ///   * числа устья — `PipeFittingSpec.PortOffsetMm` / `PortAxis` и длина трубы;
    ///   * элемент отвечает за своё устье сам — `ISnapPorts.SnapPortAt`;
    ///   * из него читают ВСЕ: снимок сцены (`ScenePipeSnapshot` через
    ///     `PortPositionUnits`/`PortDirection`), снимок геометрии
    ///     (`ElementGeometryExtensions` → `ElementGeometry.Ports`), а через него —
    ///     отбор кандидатов, изменение размера и диагностика.
    ///
    /// Два скана ниже держат обе половины: кто имеет право СЧИТАТЬ устье и кто имеет
    /// право знать правило посадки по нему. Оба со списком разрешённых файлов, у
    /// каждого — причина: без причины через полгода не отличить осознанное исключение
    /// от недосмотра.</summary>
    public class SnapPortRuleSingleSourceTests
    {
        private static readonly Regex MouthArithmetic =
            new Regex(@"PortOffsetMm|\.PortAxis\s*\(");

        private static readonly Regex SeatTakesTheCursor =
            new Regex(@"SeatAfterMove\s*\([^;\r\n]*_dragCursor");

        private static readonly Regex SeatRule =
            new Regex(@"\b(SnapPortSeat|SnapPortDock|PortedPart|SnapPortAt|SnapPortCount"
                      + @"|ISnapPorts|HasPorts|SnapPort)\b");

        private static readonly (string file, string why)[] MayComputeAMouth =
        {
            ("PipeFittingSpec.cs", "чистые числа фитинга: смещение устья и ось ножки — это и есть источник"),
            ("PipeFittingElement.cs", "единственный элемент, переводящий эти числа в позу; отсюда читают и снимок сцены, и снимок геометрии"),
            ("PipePartHighlight.cs", "красная накладка садится ровно НА устье: полоса 10 мм отмеряется ОТ него внутрь ноги. Точку устья файл ЧИТАЕТ у PipeFittingSpec.PortOffsetMm, направление ноги выводит из пары «ступица — устье», и собственной арифметики оси и длины ноги не пишет: разойдись она с настоящим устьем — человек чинил бы правильный стык, глядя на полосу, приклеенную мимо"),
            ("PipeFittingSeatChoice.cs", "перебирает КАНДИДАТОВ посадки (порт × доворот на 90°) ДО того, как один из них выбран и применён: чтобы посчитать, сколько ссылок даст каждый кандидат, обязан знать те же смещения устьев, что и сам элемент — читает их через PipeFittingSpec.PortOffsetMm, а не выводит заново своей арифметикой"),
        };

        private static readonly (string file, string why)[] MayKnowTheSeatRule =
        {
            ("SnapPort.cs", "устье как ДАННЫЕ — точка и направление, решений не принимает"),
            ("SnapPortSeat.cs", "здесь правило и живёт: пара устьев, ранг встречности, сдвиг"),
            ("SnapPortDock.cs", "вторая половина того же правила — ДОВОРОТ: то же устье в устье, но с поворотом и с конкуренцией по курсору; отдельный файл потому, что покадровый путь его не платит, а посадка при отпускании кнопки платит"),
            ("PortedPart.cs", "часть сцены, у которой есть устья, как ДАННЫЕ: доворот читает только устья и не строит ни граней, ни габаритов"),
            ("PipeDocking.cs", "единственный переводчик доворота в позу сцены: ось и угол из ядра превращаются в SetPositionAndRotation"),
            ("ElementGeometry.cs", "снимок объявляет устья как данные"),
            ("ElementGeometryExtensions.cs", "единственный переводчик сцены в снимок: тут решается, у кого устья вообще есть"),
            ("ISnapPorts.cs", "объявляет устья как СПОСОБНОСТЬ элемента — так правило не превращается в лестницу типов"),
            ("PipeElement.cs", "труба отвечает за свои торцы сама"),
            ("PipeFittingElement.cs", "фитинг отвечает за свои устья сам"),
            ("SnapCore.cs", "отбор кандидатов ЗОВЁТ правило — своего условия не пишет"),
            ("SnapNeighbourFacts.cs", "оракул зовёт ТУ ЖЕ функцию: иначе snap_diagnose начнёт врать раньше, чем сломается код"),
            ("ResizeSnap.cs", "вторая реализация той же геометрии зовёт ту же функцию"),
            ("SnapSystem.cs", "перекладывает замер посадки в поля отчёта snap_diagnose"),
            ("PipeRunFollow.cs", "обратное направление той же связи: не устье едет к трубе, а труба тянется за устьем. Устья он ЧИТАЕТ (SnapPortAt) и ни одного условия посадки не повторяет — кто с кем соединён, спрашивает у PipeSurvey"),
            ("PipeEndFittings.cs", "и новая деталь на свободном порту, и смена вида на занятом обязаны посадить деталь устье в устье, не переизобретая доворот: свободный порт он читает через ISnapPorts.SnapPortAt, лучший поворот берёт у PipeFittingSeatChoice.BestForNewFitting, связь при смене вида сохраняет PipeKindSwap.AnchorPortIndex, а готовое устье в обоих случаях передаёт в PipeDocking.SeatPort — само правило доворота не пишет"),
            ("MmGrid.cs", "не считает и не переопределяет правило посадки — спрашивает лишь СПОСОБНОСТЬ ISnapPorts, чтобы вовсе не округлять габарит детали с устьями: у такой детали минимум бокса структурно дробный, и независимое округление рвёт уже закрытый стык (см. ScenePipeJointGridRepairTests)"),
        };

        private static string CoreSourceDir() => RepoPaths.Subdir("Assets", "Scripts", "Core");

        private static string[] Sources() =>
            Directory.GetFiles(CoreSourceDir(), "*.cs", SearchOption.AllDirectories);

        private static List<string> Offenders(Regex rule, (string file, string why)[] allowed)
        {
            var offenders = new List<string>();
            foreach (var path in Sources())
            {
                string name = Path.GetFileName(path) ?? string.Empty;
                if (allowed.Any(a => a.file == name)) continue;
                if (!rule.IsMatch(File.ReadAllText(path))) continue;
                offenders.Add(name);
            }
            return offenders;
        }

        [Test]
        public void TheScan_SeesTheCore_AndIsNotSilentlyEmpty()
        {
            Assert.Greater(Sources().Length, 100,
                "обход ушёл мимо каталога: пустой список файлов делает сторожа вечно "
                + "зелёным и ничего не проверяющим");
        }

        [Test]
        public void TheMouthArithmetic_IsDone_OnlyByTheFilesAllowedToDoIt()
        {
            Assert.IsEmpty(Offenders(MouthArithmetic, MayComputeAMouth),
                "устье порта посчитано ещё раз в: "
                + string.Join(", ", Offenders(MouthArithmetic, MayComputeAMouth))
                + ". Это правило проекта, и соблюсти его — часть той же работы, ничьего "
                + "разрешения на это не нужно. Второе описание устья расходится с первым "
                + "МОЛЧА: снэп сядет в одну точку, ScenePipeSnapshot увидит другую, "
                + "PIP-01 объявит конец открытым на собранной трассе, и ни один тест не "
                + "покраснеет. Спрашивайте устье у элемента — ISnapPorts.SnapPortAt, — а "
                + "не считайте его заново. Если файл действительно обязан считать устье, "
                + "впишите его в MayComputeAMouth вместе с причиной");
        }

        [Test]
        public void TheSeatRule_IsNamed_OnlyByTheFilesAllowedToKnowIt()
        {
            Assert.IsEmpty(Offenders(SeatRule, MayKnowTheSeatRule),
                "правило посадки по устьям описано ещё раз в: "
                + string.Join(", ", Offenders(SeatRule, MayKnowTheSeatRule))
                + ". Правило про грани уже один раз разошлось между отбором и "
                + "диагностикой и стоило 78 ложных находок — зовите SnapPortSeat.Best / "
                + "BestOf / TryAlongNormal, а не повторяйте условие. Если файл обязан его "
                + "называть — впишите его в MayKnowTheSeatRule вместе с причиной, и пусть "
                + "решение будет видно");
        }

        [Test]
        public void TheSceneSnapshot_TakesItsMouthsFromTheElement_NotFromItsOwnArithmetic()
        {
            string? path = Sources().FirstOrDefault(p =>
                Path.GetFileName(p) == "ScenePipeSnapshot.cs");
            Assert.IsNotNull(path, "снимок сцены исчез — сторож остался бы зелёным ни о чём");

            string text = File.ReadAllText(path!);

            Assert.IsFalse(MouthArithmetic.IsMatch(text),
                "положительный контроль к скану выше на КОНКРЕТНОМ файле, ради которого "
                + "он написан: до этой работы про порты знал только ScenePipeSnapshot, и "
                + "соблазн посчитать устье прямо здесь — главный способ завести второй "
                + "источник");
            StringAssert.Contains("PortPositionUnits", text,
                "и он обязан читать устье у элемента: тогда снэп и трасса видят ОДНУ "
                + "точку по построению, а не по совпадению");
        }

        [Test]
        public void TheDragCursor_ReachesTheSeat_FromTheMover()
        {
            string? path = Sources().FirstOrDefault(p =>
                Path.GetFileName(p) == "ElementMover.cs");
            Assert.IsNotNull(path, "перетаскивание исчезло — сторож остался бы зелёным ни о чём");

            string text = File.ReadAllText(path!);

            StringAssert.Contains("SnapCursor", text,
                "конкуренцию за доворот решает КУРСОР, и взять его негде, кроме мыши: "
                + "экранных координат не видит ни SnapSystem, ни SnapCore. Если "
                + "ElementMover перестанет его передавать, IAutoSeated.SeatAfterMove "
                + "получит SnapCursor.None, отбор молча вернётся к «ближайшая по зазору», "
                + "и ни один тест правила не покраснеет — оно само по себе останется "
                + "верным");
            Assert.IsTrue(SeatTakesTheCursor.IsMatch(text),
                "и передавать его обязана именно посадка при отпускании кнопки: "
                + "необязательный параметр SnapCursor компилируется и без аргумента. "
                + "Сторож спрашивает про АРГУМЕНТ, а не про выражение рядом с ним: "
                + "список сцены для посадки перестал быть просто PartRegistry.GetAll() "
                + "(трубы, поехавшие за прикреплённым фитингом, из него убираются — "
                + "иначе посадка утащила бы фитинг обратно на торец), и сторож, "
                + "замороживший ВЕСЬ вызов, краснел бы на изменении, к курсору "
                + "отношения не имеющем");
        }

        [Test]
        public void EveryAllowedEntry_NamesAFile_ThatStillExists()
        {
            var names = new HashSet<string>(
                Sources().Select(p => Path.GetFileName(p) ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            var gone = MayComputeAMouth.Concat(MayKnowTheSeatRule)
                .Where(a => !names.Contains(a.file)).Select(a => a.file).ToList();

            Assert.IsEmpty(gone,
                "запись пережила свой файл и теперь молча освобождает следующий, занявший "
                + "это имя: " + string.Join(", ", gone));
        }

        [Test]
        public void EveryAllowedFile_ActuallyNamesTheRule_SoNoPermissionIsACover()
        {
            var silent = new List<string>();
            foreach (var (file, rule) in Pairs())
            {
                string? path = Sources().FirstOrDefault(p => Path.GetFileName(p) == file);
                if (path == null) continue;
                if (!rule.IsMatch(File.ReadAllText(path))) silent.Add(file);
            }

            Assert.IsEmpty(silent,
                "файл разрешён называть правило, но не называет его — разрешение стало "
                + "прикрытием для следующего, кто впишет туда своё условие: "
                + string.Join(", ", silent));
        }

        private static IEnumerable<(string file, Regex rule)> Pairs()
        {
            foreach (var (file, _) in MayComputeAMouth) yield return (file, MouthArithmetic);
            foreach (var (file, _) in MayKnowTheSeatRule) yield return (file, SeatRule);
        }
    }
}
