using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Tests.Geometry;

/// <summary>Единицы ОТВЕТОВ не проверял никто. McpContractSurfaceTests сканирует
/// только входы, McpResponseFieldGuideParityTests — только наличие прозы и только для
/// ElementInfo, а половина поверхности отдаётся анонимными объектами прямо из
/// обработчиков, куда рефлексия не достаёт вовсе. Именно там метры и прожили дольше
/// всего: posX рядом с dimX, minX рядом с sizeMmX, boundsCenter рядом с boundsSizeMM.
///
/// Здесь два скана, потому что поверхность двойная: DTO ловятся рефлексией, а
/// анонимные объекты — только чтением исходника. Каждый скан проверяет сам себя:
/// сканер, который ничего не нашёл, зеленеет, не проверив ничего.</summary>
public class McpResponseUnitContractTests
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>Единица, названная в имени поля. Сравнение без учёта регистра: в
    /// поверхности живут и MM (edgeThicknessMM), и Mm (posXMm).</summary>
    private static readonly string[] UnitTokens =
        { "mm", "deg", "pct", "px", "sec", "m2", "count", "ratio", "progress", "index" };

    /// <summary>Числа ответа, у которых физической единицы НЕТ. Причина обязательна:
    /// без неё через полгода не отличить безразмерную величину от забытого метра.
    /// EveryExemption_StillNamesALivingField следит, чтобы запись не пережила поле.</summary>
    private static readonly Dictionary<string, string> NumbersWithoutAUnit =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ElementInfo.moduleId"] = "идентификатор модуля, а не величина",
            ["ElementInfo.faceNormalX"] = "единичная нормаль грани — безразмерна",
            ["ElementInfo.faceNormalY"] = "то же самое",
            ["ElementInfo.faceNormalZ"] = "то же самое",
            ["FaceInfo.normalX"] = "единичная нормаль грани — безразмерна",
            ["FaceInfo.normalY"] = "то же самое",
            ["FaceInfo.normalZ"] = "то же самое",
            ["ObjectInfo.scaleX"] = "Transform.localScale — безразмерный множитель",
            ["ObjectInfo.scaleY"] = "то же самое",
            ["ObjectInfo.scaleZ"] = "то же самое",
            ["ModuleInfo.id"] = "идентификатор модуля, а не величина",
            ["LightSwitchInfo.maxLights"] = "потолок числа ламп — штуки",
            ["SnapNeighborReport.bestDot"] = "скалярное произведение нормалей — безразмерно",
        };

    /// <summary>Типы, живущие в пространстве имён MCP, но провода не видящие. Причина
    /// обязательна: «оно не ответ» — это утверждение, которое стареет.</summary>
    private static readonly Dictionary<string, string> NotAResponseDto =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AabbInfo"] = "внутренний бокс в юнитах Unity: наружу уходит только через "
                + "McpAnchor.ToMmBox, то есть уже как AabbMmInfo",
            ["CompiledFloorplan"] = "выход FloorplanCompiler; apply_floorplan отдаёт счётчики",
            ["CompiledPlanWall"] = "то же самое",
            ["CompiledPlanFloor"] = "то же самое",
            ["CompiledPlanRoom"] = "то же самое",
        };

    /// <summary>Основы имён, которые ВСЕГДА обозначают длину. Анонимный объект не
    /// виден рефлексии, а тип поля из исходника не вывести — зато имя видно, и
    /// именно имя есть контракт. Список закрыт намеренно: он ловит класс дефекта
    /// («длина без единицы»), а не пытается вывести тип.</summary>
    private static readonly string[] LengthStems =
    {
        "pos", "dim", "size", "min", "max", "center", "centre", "anchor", "offset",
        "radius", "length", "width", "height", "depth", "distance", "thickness", "gap",
        "spacing", "inset", "protrusion", "reach", "clearance",
    };

    /// <summary>Имена в объектных инициализаторах, которые несут основу длины, но длиной
    /// не являются, — с причиной на каждое. Ключ включает файл: одно и то же слово в
    /// разных местах значит разное.</summary>
    private static readonly Dictionary<string, string> NotALengthAfterAll =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ElementInfoBuilder.cs :: faceGaps"] = "список AxisGapInfo, каждый со своим gapMM",
            ["ElementInfoBuilder.cs :: fixedSize"] = "флаг «размер задан производителем»",
            ["ElementInfoBuilder.cs :: maxLights"] = "потолок числа ламп — штуки",
            ["ElementInfoBuilder.cs :: size"] = "bed.SizeName — строка «160x200», а не число",
            ["ElementInfoBuilder.cs :: sizeId"] = "pipe.SizeId — идентификатор строки ГОСТ "
                + "3262-75 («dn20»), а не длина: сами размеры трубы уходят рядом и с "
                + "единицей — nominalBoreMM, outerDiameterMM, wallThicknessMM",
            ["McpCommandHandler.Bulk.cs :: widthAxis"] = "ось ширины модуля — буква x или z",
            ["McpCommandHandler.Helpers.cs :: widthAxis"] = "то же самое",
            ["McpCommandHandler.Elements.Query.cs :: gaps"] = "список AxisGapInfo",
            ["McpAabb.cs :: minX"] = "ВНУТРЕННИЙ AabbInfo в юнитах Unity: наружу бокс уходит "
                + "только через McpAnchor.ToMmBox, то есть уже как AabbMmInfo",
            ["McpAabb.cs :: minY"] = "то же самое",
            ["McpAabb.cs :: minZ"] = "то же самое",
            ["McpAabb.cs :: maxX"] = "то же самое",
            ["McpAabb.cs :: maxY"] = "то же самое",
            ["McpAabb.cs :: maxZ"] = "то же самое",
            ["McpCommandHandler.Elements.Query.cs :: minX"] = "тот же внутренний AabbInfo в "
                + "get_free_space: он считает перекрытия, а в ответ уходят minXMm/maxXMm",
            ["McpCommandHandler.Elements.Query.cs :: minY"] = "то же самое",
            ["McpCommandHandler.Elements.Query.cs :: minZ"] = "то же самое",
            ["McpCommandHandler.Elements.Query.cs :: maxX"] = "то же самое",
            ["McpCommandHandler.Elements.Query.cs :: maxY"] = "то же самое",
            ["McpCommandHandler.Elements.Query.cs :: maxZ"] = "то же самое",
            ["McpCommandHandler.PlanGeometry.cs :: width"] = "не ответ, а сборка параметров "
                + "add_opening внутри apply_floorplan. Долг записан и не благословлён: "
                + "плановая декларация до сих пор пишет width/height без _mm, и её "
                + "переименование — своя правка, а не побочный эффект этой",
            ["McpCommandHandler.PlanGeometry.cs :: height"] = "то же самое",
            ["FloorplanCompiler.cs :: height"] = "поле скомпилированного плана, внутренняя "
                + "структура компилятора, наружу не уходит",
            ["FloorplanCompiler.cs :: thickness"] = "то же самое",
        };

    private static readonly Regex ObjectInitializerStart =
        new Regex(@"\bnew\b[^;(){}\[\]]*\{", RegexOptions.Compiled);

    private static readonly Regex InitializerField =
        new Regex(@"(?<![\w.\]])(?<name>[a-z][A-Za-z0-9_]*)\s*=(?!=)", RegexOptions.Compiled);

    /// <summary>Имена полей объектных инициализаторов — и анонимных, и типизованных.
    /// Локальные переменные под это не попадают: сканируются только области между
    /// «new … {» и парной закрывающей скобкой.</summary>
    private static IEnumerable<string> InitializerFieldNames(string source)
    {
        foreach (Match start in ObjectInitializerStart.Matches(source))
        {
            int open = start.Index + start.Length - 1;
            int depth = 0, i = open;
            for (; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}') { depth--; if (depth == 0) break; }
            }
            if (depth != 0) continue;
            var body = source.Substring(open + 1, i - open - 1);
            foreach (Match field in InitializerField.Matches(body))
                yield return field.Groups["name"].Value;
        }
    }

    private static string McpSourceDir() => RepoPaths.Subdir("Assets", "Scripts", "Core", "MCP");

    private static bool NamesAUnit(string field) =>
        UnitTokens.Any(u => field.IndexOf(u, StringComparison.OrdinalIgnoreCase) >= 0);

    private static bool LooksLikeALength(string field)
    {
        var lower = field.ToLowerInvariant();
        return LengthStems.Any(stem => lower.Contains(stem, StringComparison.Ordinal));
    }

    private static List<Type> ResponseDtoTypes() =>
        typeof(ElementInfo).Assembly.GetTypes()
            .Where(t => t.IsClass && t.IsPublic)
            .Where(t => t.Namespace == "KitchenDesigner.Core.MCP")
            .Where(t => t != typeof(McpRequest) && t != typeof(McpResponse))
            .Where(t => !NotAResponseDto.ContainsKey(t.Name))
            .Concat(new[] { typeof(SnapDiagnosis), typeof(SnapNeighborReport) })
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    private static bool IsNumeric(Type t)
    {
        var bare = Nullable.GetUnderlyingType(t) ?? t;
        if (bare.IsArray) return IsNumeric(bare.GetElementType()!);
        return bare == typeof(int) || bare == typeof(float) || bare == typeof(double);
    }

    /// <summary>Только обработчики: Contract/ — это ВХОД, за ним следит
    /// McpContractSurfaceTests, а McpGuideTexts вообще проза.</summary>
    private static IEnumerable<string> SourceFiles() =>
        Directory.GetFiles(McpSourceDir(), "*.cs", SearchOption.TopDirectoryOnly);

    [Test]
    public void BothScans_SeeTheSurface_AndTheNameRulesActuallyDiscriminate()
    {
        Assert.Greater(ResponseDtoTypes().Count, 20,
            "DTO ответов не нашлись — сканер ослеп, а не позеленел");
        Assert.Greater(SourceFiles().Count(), 10,
            "исходники MCP не нашлись — сканер анонимных объектов читает пустоту");

        Assert.IsTrue(NamesAUnit("posXMm"), "Mm в конце имени обязан считаться единицей");
        Assert.IsTrue(NamesAUnit("edgeThicknessMM"), "старое написание MM тоже единица");
        Assert.IsFalse(NamesAUnit("posX"), "имя без единицы принято за именованное — "
            + "тогда сторож зелен по всей метровой поверхности разом");
        Assert.IsTrue(LooksLikeALength("posX"), "pos — основа длины");
        Assert.IsTrue(LooksLikeALength("boundsCenter"), "center внутри имени тоже считается");
        Assert.IsFalse(LooksLikeALength("hasViolations"), "флаг принят за длину");
        Assert.IsFalse(LooksLikeALength("name"), "имя принято за длину");

        var probe = "class X { void M() { var a = new { posX = 1, ok = true }; int minX = 0; } }";
        var found = InitializerFieldNames(probe).ToList();
        CollectionAssert.Contains(found, "posX",
            "разбор объектного инициализатора не видит его полей — второй скан слеп");
        CollectionAssert.DoesNotContain(found, "minX",
            "в улов попала ЛОКАЛЬНАЯ переменная: тогда сканер завалит список ложными "
            + "срабатываниями, их занесут в исключения, и настоящий метр проедет вместе с ними");
    }

    [Test]
    public void EveryNumberOfAResponseDto_NamesItsUnitInTheFieldName()
    {
        var unitless = new List<string>();

        foreach (var type in ResponseDtoTypes())
            foreach (var field in type.GetFields(PublicInstance))
            {
                if (!IsNumeric(field.FieldType)) continue;
                var key = type.Name + "." + field.Name;
                if (NumbersWithoutAUnit.ContainsKey(key)) continue;
                if (NamesAUnit(field.Name)) continue;
                unitless.Add(key);
            }

        CollectionAssert.IsEmpty(unitless,
            "мм — единственная единица провода, и живёт она в ИМЕНИ поля, а не в прозе "
            + "guide: агент читает JSON, а не описание. Число ответа без единицы в имени "
            + "агент трактует наугад, и ошибка тихая — posX:0.715 рядом с dimX:564 выглядит "
            + "как один объект в одних единицах. Допиши единицу в имя (Mm, Deg, Pct, Px, "
            + "Sec, M2, Count) или занеси поле в NumbersWithoutAUnit с причиной, если "
            + "величина действительно безразмерна:\n" + string.Join("\n", unitless));
    }

    [Test]
    public void EveryLengthOfAnInlineResponse_NamesItsUnitInTheFieldName()
    {
        var unitless = new List<string>();

        foreach (var path in SourceFiles())
        {
            var name = Path.GetFileName(path);
            foreach (var field in InitializerFieldNames(File.ReadAllText(path)))
            {
                if (!LooksLikeALength(field)) continue;
                if (NamesAUnit(field)) continue;
                var key = name + " :: " + field;
                if (NotALengthAfterAll.ContainsKey(key)) continue;
                if (!unitless.Contains(key)) unitless.Add(key);
            }
        }

        CollectionAssert.IsEmpty(unitless,
            "половина ответов собирается анонимными объектами прямо в обработчике, куда "
            + "рефлексия не достаёт: get_free_space, get_floor_info, edit_elements results, "
            + "get. Метры прожили там дольше всего именно поэтому. Имя, начинающееся с "
            + "основы длины (pos, dim, size, min, max, center, anchor, offset, ...), обязано "
            + "нести единицу — либо это не длина, и тогда её место в NotALengthAfterAll с "
            + "причиной:\n" + string.Join("\n", unitless));
    }

    [Test]
    public void EveryExemption_StillNamesALivingField()
    {
        var live = ResponseDtoTypes()
            .SelectMany(t => t.GetFields(PublicInstance).Select(f => t.Name + "." + f.Name))
            .ToList();

        foreach (var pair in NumbersWithoutAUnit)
            CollectionAssert.Contains(live, pair.Key,
                "исключение «" + pair.Key + "» (" + pair.Value + ") пережило своё поле: "
                + "запись начнёт молча освобождать следующее поле с этим именем");

        var liveTypes = typeof(ElementInfo).Assembly.GetTypes().Select(t => t.Name).ToList();
        foreach (var pair in NotAResponseDto)
            CollectionAssert.Contains(liveTypes, pair.Key,
                "исключение «" + pair.Key + "» (" + pair.Value + ") пережило свой тип");

        var raw = new List<string>();
        foreach (var path in SourceFiles())
        {
            var name = Path.GetFileName(path);
            foreach (var field in InitializerFieldNames(File.ReadAllText(path)))
                if (LooksLikeALength(field) && !NamesAUnit(field))
                    raw.Add(name + " :: " + field);
        }
        foreach (var pair in NotALengthAfterAll)
            CollectionAssert.Contains(raw, pair.Key,
                "исключение «" + pair.Key + "» (" + pair.Value + ") больше не срабатывает: "
                + "поле переименовали или удалили, а прощение осталось и ждёт следующее "
                + "имя с той же основой");
    }
}
