using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Сторож правила «декор мостится, он не растягивается»
    /// (CONVENTIONS.md → «CRITICAL: a decor tiles, it never stretches»).
    ///
    /// Как это устроено на самом деле — и почему словесная формулировка «никаких
    /// нормализованных UV» промахивается мимо кода: меш строит UV в 0..1, а
    /// повтор плитки задаёт MaterialManager.RefreshTiling через BaseMap_ST =
    /// DecorSurfaceMM / TileMM. То есть 0..1 — это ПРАВИЛЬНО, но ровно при одном
    /// условии: UV обязаны разворачиваться по тем самым двум осям, которые
    /// называет DecorSurfaceMM. Базовая реализация возвращает (dims.x, dims.y) —
    /// это верно для стоячей панели и неверно для горизонтальной столешницы, где
    /// вторая ось — глубина. Растянутая столешница уехала в релиз именно так.
    ///
    /// Отсюда механическая проверка: класс, который строит СВОЙ меш, обязан сам
    /// объявить DecorSurfaceMM — иначе он молча наследует оси стоячей панели.
    /// Исключения перечислены поимённо, у каждого причина; PillarElement —
    /// записанный долг (CONVENTIONS.md → «Known debts»).</summary>
    public class DecorSurfaceUvTests
    {
        /// <summary>Класс строит свой меш либо напрямую (<c>XxxMesh.Build</c>), либо
        /// поручая это общему помощнику. Второй случай приписан сюда намеренно: когда
        /// конвейер «профиль → меш → коллайдер» уехал из табуретки и радиусного стола в
        /// <c>TabletopSurface</c>, оба разом выпали из скана, и правило перестало их
        /// видеть, не сказав ни слова. Владелец помощника отвечает за оси развёртки
        /// ровно так же, как если бы строил меш сам.</summary>
        private const string BuildsItsOwnMesh =
            @"\b\w+Mesh\s*\.\s*Build\s*\(|\bnew\s+TabletopSurface\s*\(";

        /// <summary>Помощники, строящие меш ДЛЯ элемента. Они не элементы и своей
        /// поверхности декора не имеют — её называет владелец, и скан ищет владельца.</summary>
        private static readonly string[] MeshBuildingHelpers =
            { "TabletopSurface.cs", "FurniturePartSet.cs" };

        private const string DeclaresDecorSurface = @"\bVector2Int\s+DecorSurfaceMM\b";

        private const string WritesUvs = @"mesh\s*\.\s*uv\s*=|SetUVs\s*\(";

        /// <summary>Классы, строящие свой меш и НЕ объявляющие свою поверхность
        /// декора, с причиной. Существование каждого файла проверяет
        /// <see cref="AllowList_NamesOnlyFilesThatExist"/>.</summary>
        private static readonly (string file, string why)[] AllowedWithoutDecorSurface =
        {
            ("AssembledFacadeElement.cs",
                "сборный фасад стоит вертикально: UV разворачиваются по (x, y), и базовые "
                + "оси DecorSurfaceMM для него верны"),
            ("DrawerElement.cs",
                "короб ящика скрыт за фасадом и декор не носит; фасад — отдельный элемент"),
            ("PillarElement.cs",
                "ДОЛГ (CONVENTIONS.md → «Known debts»): у CylinderStackMesh бок развёрнут как "
                + "i/Segments по ОКРУЖНОСТИ, а торцы как cos*0.5+0.5 по диаметру — ни то ни "
                + "другое не совпадает с (dims.x, dims.y), и декор на колонне растянут в pi раз"),
            ("ScrewLegElement.cs",
                "ДОЛГ, тот же самый: винтовая опора собрана из тех же цилиндров "
                + "CylinderStackMesh, что и колонна, и наследует её развёртку по окружности. "
                + "Опора — металлическая фурнитура и носит однотонный цвет, поэтому долг "
                + "закрывается вместе с колонной, а не отдельно"),
        };

        /// <summary>Каждый меш-строитель, пишущий UV, обязан быть здесь: новый
        /// строитель обязан объяснить, по каким осям он разворачивает деталь,
        /// прежде чем попасть в сцену. Это и есть тот шаг, которого не было в
        /// чек-листе нового элемента, когда уехала растянутая столешница.</summary>
        private static readonly (string file, string uvSpace)[] UvBuilders =
        {
            ("AssembledFacadeMesh.cs", "0..1 по (ширина, высота) стоячего фасада"),
            ("BasinMesh.cs", "0..1 по (ширина, ГЛУБИНА) плана — ОДНА развёртка сверху на "
                + "весь меш, ровно как у SoftSlabMesh: ванна лежит горизонтально, и борт, и "
                + "стенка чаши, и дно проецируются на один и тот же план. Владелец (ванна) "
                + "объявляет DecorSurfaceMM в (x, z), и оси совпадают. Вертикальная стенка "
                + "при такой развёртке вырождается в узкую полосу — это осознанная плата: "
                + "акрил однотонный, плитку на борт ванны не мостят"),
            ("CushionMesh.cs", "0..1 по двум касательным осям КАЖДОЙ из шести граней "
                + "скруглённого куба: подушка замкнута со всех сторон, единой развёртки на "
                + "весь меш у неё быть не может, и вершины на швах дублируются именно ради "
                + "этого. Оси граней совпадают с осями детали, поэтому DecorSurfaceMM "
                + "владельца (дивана) остаётся верным"),
            ("DrawerMesh.cs", "0..1 по граням единичного короба; декора не носит"),
            ("DrumRecessMesh.cs", "корпус стиральной/сушильной машины: ЛИЦЕВАЯ грань с "
                + "кольцом вокруг устья барабана развёрнута 0..1 по (ширина, высота) стоячей "
                + "детали — ровно те оси, которые LaundryMachineElement объявляет в "
                + "DecorSurfaceMM, поэтому декор на фасаде машины мостится. Остальные "
                + "внешние грани — 0..1 по своим осям, как у GrooveMesh. Стенка расточки "
                + "развёрнута по (окружность, глубина) — тот же ДОЛГ, что у CylinderStackMesh, "
                + "и терпимый по той же причине: внутренность барабана металлическая и "
                + "декора не носит"),
            ("FloorPolygonMesh.cs", "МИРОВЫЕ мм X/Z, делённые на габарит контура: пол лежит "
                + "горизонтально, поэтому FloorElement объявляет DecorSurfaceMM = (x, z), а ноль "
                + "развёртки взят в начале мира — точка привязки пола ездит при правке контура "
                + "(FloorDecorUvTests)"),
            ("GrooveMesh.cs", "0..1 по граням единичного щита — базовый случай"),
            ("CylinderStackMesh.cs", "ДОЛГ: окружность и диаметр вместо осей DecorSurfaceMM — "
                + "общая стопка цилиндров под колонну и винтовую опору"),
            ("MeshAccumulator.cs", "0..1 по (окружность, длина трассы) протяжки трубы: сантехника "
                + "собрана из труб, и её развёртка идёт вдоль оси кольцами. ДОЛГ того же рода, "
                + "что у CylinderStackMesh, и по той же причине терпимый — смеситель и стойка "
                + "носят однотонный металл, плитку на них не мостят"),
            ("PlaneWithHolesMesh.cs", "ФИЗИЧЕСКИЕ мм / TileMM: накладка рисуется собственным "
                + "мешем и BaseMap_ST к ней не применяется, поэтому мостит сама"),
            ("SoftSlabMesh.cs", "0..1 по (ширина, ГЛУБИНА) плана — ОДНА развёртка сверху на "
                + "весь меш, как у ProfileExtrusionMesh: мягкая плита лежит горизонтально, "
                + "её главные поверхности — верх и низ, а узкая полоса скруглённой кромки "
                + "проецируется на тот же план. Владелец (пуфик) объявляет DecorSurfaceMM "
                + "в (x, z), и оси совпадают"),
            ("ProfileExtrusionMesh.cs", "ОДНА развёртка на весь меш по (ширина, ГЛУБИНА): "
                + "крышки — след детали (x/Ш + 0,5; z/Г + 0,5), боковины — длина дуги вдоль "
                + "контура/Ш по u и толщина/Г по v. Поэтому и радиусная полка, и радиусный "
                + "стол объявляют DecorSurfaceMM в (x, z), и декор на них мостится "
                + "физическими миллиметрами и на пласти, и на торце"),
        };

        private static string ElementsDir() => RepoPaths.Subdir("Assets", "Scripts", "Core", "Elements");

        private static string[] ElementSources() =>
            Directory.GetFiles(ElementsDir(), "*.cs", SearchOption.AllDirectories);

        private static bool Matches(string file, string what) =>
            File.ReadAllLines(file).Any(line => Regex.IsMatch(SourceLines.CodeOnly(line), what));

        public static string[] MeshBuildingElements() =>
            ElementSources()
                .Where(f => !(Path.GetFileName(f) ?? string.Empty).EndsWith("Mesh.cs", StringComparison.Ordinal))
                .Where(f => !MeshBuildingHelpers.Contains(Path.GetFileName(f) ?? string.Empty,
                    StringComparer.Ordinal))
                .Where(f => Matches(f, BuildsItsOwnMesh))
                .Select(f => Path.GetFileName(f) ?? string.Empty)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();

        private static string[] UvWritingBuilders() =>
            ElementSources()
                .Where(f => Matches(f, WritesUvs))
                .Select(f => Path.GetFileName(f) ?? string.Empty)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();

        [Test]
        public void EveryElementWithItsOwnMesh_NamesItsDecorSurface()
        {
            var exempt = new HashSet<string>(AllowedWithoutDecorSurface.Select(a => a.file),
                StringComparer.Ordinal);
            var offenders = new List<string>();

            foreach (var name in MeshBuildingElements())
            {
                if (exempt.Contains(name)) continue;
                var path = Path.Combine(ElementsDir(), name);
                if (!Matches(path, DeclaresDecorSurface)) offenders.Add(name);
            }

            Assert.IsEmpty(offenders,
                "Класс строит свой меш, но берёт оси поверхности декора у базового класса — "
                + "а там (dims.x, dims.y), то есть СТОЯЧАЯ панель. На горизонтальной детали "
                + "вторая ось — глубина, и декор растягивается вместо мощения "
                + "(CONVENTIONS.md → «a decor tiles, it never stretches»). Объяви "
                + "DecorSurfaceMM по осям своей развёртки. Найдено:\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void EveryUvBuilder_IsClassified()
        {
            var declared = UvBuilders.Select(b => b.file).OrderBy(f => f, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(declared, UvWritingBuilders(),
                "Появился (или исчез) меш-строитель, пишущий UV. Впиши его в UvBuilders и "
                + "скажи, по каким осям он разворачивает деталь: именно этого шага не было в "
                + "чек-листе нового элемента, когда уехала растянутая столешница");

            foreach (var (file, uvSpace) in UvBuilders)
                Assert.IsNotEmpty(uvSpace, "у строителя " + file + " не описана развёртка");
        }

        [Test]
        public void TheScan_ActuallySeesTheElements_AndTheKnownDebtIsInIt()
        {
            var built = MeshBuildingElements();
            Assert.IsNotEmpty(built,
                "скан не нашёл ни одного элемента со своим мешем — грепу по несуществующему "
                + "пути нечего найти, и он зеленеет, ничего не проверив");

            CollectionAssert.Contains(built, "PillarElement.cs",
                "известный долг обязан попадать в скан, иначе исключение для него ничего "
                + "не значит");
            CollectionAssert.Contains(built, "RadialShelfElement.cs");
            CollectionAssert.Contains(built, "RadiusTableElement.cs",
                "исправленный случай (растянутая столешница) обязан оставаться в скане: он "
                + "здесь положительный контроль");
            CollectionAssert.Contains(built, "StoolElement.cs",
                "мебель с профильной крышкой строит меш через TabletopSurface — если скан "
                + "видит только прямой вызов XxxMesh.Build, вся эта семья выпадает из "
                + "правила молча");
            CollectionAssert.Contains(built, "FloorElement.cs",
                "второй исправленный случай: у пола не было UV вовсе, и он молча наследовал "
                + "оси стоячей панели — то есть делил развёртку на толщину плиты");

            var exempt = AllowedWithoutDecorSurface.Select(a => a.file).ToArray();
            CollectionAssert.DoesNotContain(exempt, "RadiusTableElement.cs",
                "белый список не вправе освобождать уже исправленный случай");
            CollectionAssert.DoesNotContain(exempt, "RadialShelfElement.cs",
                "третий исправленный случай: полка перешла на общую развёртку "
                + "ProfileExtrusionMesh и объявила оси (x, z) — долг CONVENTIONS.md → «Known debts» "
                + "по ней закрыт");
            CollectionAssert.DoesNotContain(exempt, "FloorElement.cs",
                "пол исправлен: развёртка есть и оси объявлены — исключение для него было бы "
                + "ослаблением правила");
        }

        [Test]
        public void AllowList_NamesOnlyFilesThatExist()
        {
            var names = new HashSet<string>(
                ElementSources().Select(f => Path.GetFileName(f) ?? string.Empty), StringComparer.Ordinal);

            foreach (var (file, why) in AllowedWithoutDecorSurface)
            {
                Assert.IsTrue(names.Contains(file),
                    "исключение для " + file + " (" + why + ") пережило свой файл: запись "
                    + "начнёт молча освобождать следующий файл с этим именем");
                Assert.IsNotEmpty(why, "у записи " + file + " нет причины");
            }

            foreach (var (file, _) in UvBuilders)
                Assert.IsTrue(names.Contains(file), "строитель " + file + " больше не существует");
        }
    }
}
